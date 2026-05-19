# ── assemble.R ─────────────────────────────────────────────────────────────────
#
# R port of the C# DocxAssembler class.
#
# Takes an ordered list of raw DOCX blobs (raw vectors from SQLite),
# merges them into a single DOCX, and returns the result as a raw vector.
#
# The merge strategy is identical to the C# version:
#   1. Use the first document as the base
#   2. Strip the trailing <w:sectPr> from the base body
#   3. For each subsequent document:
#        a. Extract and parse word/document.xml
#        b. Strip its <w:sectPr>
#        c. Insert a page break
#        d. Append all body elements to the base
#   4. Re-attach the original <w:sectPr> at the end
#   5. Save and repack as DOCX
#   6. Return the raw bytes
#
# Packages required:
#   xml2   — XML parsing and manipulation
#   zip    — ZIP creation  (install.packages("zip"))
#
# base R unzip() is used for extraction — no extra package needed.
# ──────────────────────────────────────────────────────────────────────────────

library(xml2)
library(zip)

# Word namespace URI — used throughout DOCX XML
W_NS <- "http://schemas.openxmlformats.org/wordprocessingml/2006/main"

# Standard DOCX namespace declarations — declared on wrappers when we
# re-parse body fragments, so prefixes like a:, wp:, pic:, r:, m:, v:,
# mc:, w14:, etc. resolve correctly when a fragment contains images,
# drawings, math, VML fallbacks, or revision marks.
DOCX_NS_DECLS <- paste(
  'xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"',
  'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"',
  'xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"',
  'xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"',
  'xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"',
  'xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math"',
  'xmlns:v="urn:schemas-microsoft-com:vml"',
  'xmlns:o="urn:schemas-microsoft-com:office:office"',
  'xmlns:w10="urn:schemas-microsoft-com:office:word"',
  'xmlns:wne="http://schemas.microsoft.com/office/word/2006/wordml"',
  'xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"',
  'xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"',
  'xmlns:wpg="http://schemas.microsoft.com/office/word/2010/wordprocessingGroup"',
  'xmlns:w14="http://schemas.microsoft.com/office/word/2010/wordml"',
  'xmlns:w15="http://schemas.microsoft.com/office/word/2012/wordml"',
  sep = " "
)


# ── Public entry point ─────────────────────────────────────────────────────────

# assemble_docx()
#
# blob_list — ordered list of raw vectors, each containing a DOCX file.
#             NULL entries are silently skipped.
#
# Returns a raw vector containing the assembled DOCX.
#
assemble_docx <- function(blob_list) {

  # Remove NULLs — nodes with no content blob are skipped
  blob_list <- Filter(Negate(is.null), blob_list)

  if (length(blob_list) == 0)
    stop("No content blobs provided for assembly.")

  # Write each blob to a temp DOCX file
  temp_paths <- lapply(seq_along(blob_list), function(i) {
    path <- tempfile(fileext = ".docx")
    writeBin(blob_list[[i]], path)
    path
  })

  on.exit({
    lapply(temp_paths, function(p) {
      if (file.exists(p)) file.remove(p)
    })
  }, add = TRUE)

  merge_docx_files(temp_paths)
}


# ── Core merge logic ───────────────────────────────────────────────────────────

# merge_docx_files()
#
# docx_paths — ordered character vector of DOCX file paths.
#
# Returns a raw vector containing the assembled DOCX.
#
merge_docx_files <- function(docx_paths) {

  ns <- c(w = W_NS)

  # ── Step 1: Extract and load the base document ───────────────────────
  base_dir <- tempfile()
  dir.create(base_dir)
  on.exit(unlink(base_dir, recursive = TRUE), add = TRUE)

  unzip(docx_paths[[1]], exdir = base_dir)

  doc_xml_path <- file.path(base_dir, "word", "document.xml")
  if (!file.exists(doc_xml_path))
    stop("document.xml not found in first DOCX — file may be corrupt.")

  main_doc <- read_xml(doc_xml_path)
  body     <- xml_find_first(main_doc, ".//w:body", ns)

  if (is.na(body))
    stop("No <w:body> element found in first document.")

  # ── Step 2: Detach the trailing sectPr from the base body ─────────────
  # sectPr holds page layout settings. We remove it temporarily and
  # re-attach it at the very end so it governs the whole assembled doc.
  sect_pr_node <- xml_find_last(body, "w:sectPr", ns)
  sect_pr_str  <- NULL

  if (!is.na(sect_pr_node)) {
    # Serialize to string before removing so we can re-insert it later
    sect_pr_str <- as.character(sect_pr_node)
    xml_remove(sect_pr_node)
  }

  # ── Step 3: Merge each subsequent document into the base body ─────────
  for (i in seq_along(docx_paths)[-1]) {

    doc_dir <- tempfile()
    dir.create(doc_dir)
    on.exit(unlink(doc_dir, recursive = TRUE), add = TRUE)

    # Skip-before-tryCatch: if the DOCX itself is malformed, we want
    # `next` to advance the loop. `return()` inside a tryCatch expression
    # returns from the enclosing function, so we must not use it here.
    unzip(docx_paths[[i]], exdir = doc_dir)
    curr_xml_path <- file.path(doc_dir, "word", "document.xml")
    if (!file.exists(curr_xml_path)) {
      warning(sprintf("document.xml not found in file %d — skipping.", i))
      next
    }

    tryCatch({

      curr_doc  <- read_xml(curr_xml_path)
      curr_body <- xml_find_first(curr_doc, ".//w:body", ns)

      # Inside the tryCatch, use stop() to abort this iteration; the
      # handler below converts it into a warning and loop moves on.
      if (is.na(curr_body))
        stop(sprintf("No <w:body> in file %d", i))

      # Remove sectPr from this document's body before merging
      curr_sect <- xml_find_last(curr_body, "w:sectPr", ns)
      if (!is.na(curr_sect)) xml_remove(curr_sect)

      # Insert a page break before this document's content.
      # The namespace must be declared on the element itself since it is
      # being created outside the context of the main document.
      page_break_xml <- sprintf(
        '<w:p xmlns:w="%s"><w:r><w:br w:type="page"/></w:r></w:p>',
        W_NS)
      xml_add_child(body, read_xml(page_break_xml))

      # Append each body element from the current document. We round-trip
      # each child through a wrapper that declares the full set of DOCX
      # namespaces, so fragments containing images, drawings, math, or
      # VML don't lose their prefix bindings during re-parse.
      for (child in xml_children(curr_body)) {
        child_xml <- sprintf(
          '<mc:root %s>%s</mc:root>',
          DOCX_NS_DECLS,
          as.character(child))
        wrapped <- read_xml(child_xml)
        copied  <- xml_children(wrapped)[[1]]
        xml_add_child(body, copied)
      }

    }, error = function(e) {
      warning(sprintf("Error merging file %d (%s): %s",
                      i, docx_paths[[i]], e$message))
    })
  }

  # ── Step 4: Re-attach the sectPr at the end ───────────────────────────
  if (!is.null(sect_pr_str)) {
    sect_pr_xml <- sprintf(
      '<mc:root %s>%s</mc:root>',
      DOCX_NS_DECLS, sect_pr_str)
    wrapped_sect <- read_xml(sect_pr_xml)
    xml_add_child(body, xml_children(wrapped_sect)[[1]])
  }

  # ── Step 5: Save the modified document.xml ────────────────────────────
  write_xml(main_doc, doc_xml_path)

  # ── Step 6: Repack as DOCX (ZIP) and return raw bytes ─────────────────
  output_path <- tempfile(fileext = ".docx")
  on.exit(if (file.exists(output_path)) file.remove(output_path), add = TRUE)

  # zip::zip requires paths relative to a working directory.
  # all.files = TRUE is critical: DOCX contains hidden dotfiles like
  # _rels/.rels and word/_rels/document.xml.rels — leave these out and
  # Word will refuse to open the result.
  old_wd <- getwd()
  setwd(base_dir)
  tryCatch(
    zip::zip(output_path,
             files = list.files(".", recursive = TRUE, full.names = FALSE,
                                all.files = TRUE, no.. = TRUE)),
    finally = setwd(old_wd)
  )

  if (!file.exists(output_path))
    stop("Output DOCX was not created — zip step failed.")

  readBin(output_path, what = "raw", n = file.info(output_path)$size)
}
