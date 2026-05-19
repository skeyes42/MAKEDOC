# =============================================================================
# AddTemplateNodes.R  (batch mode)
# Loads all Template DOCX files listed in the templates manifest into the
# Node table of the MAKEDOC database as BLOBs in the Content field.
# =============================================================================

suppressPackageStartupMessages({
  library(RSQLite)
  library(jsonlite)
})
suppressPackageStartupMessages(
  suppressWarnings(
    library(readr)
  )
)


# --- 0. Configuration --------------------------------------------------------
cfg <- fromJSON("C:/Users/skeye/BOOK2/MAKEDOC/config/makedoc.config.json")
db_path        <- cfg$DatabasePath
templates_root <- cfg$TemplatesRoot
manifest_path  <- cfg$TemplatesManifest

cat("=== MAKEDOC Template Node Importer (batch) ===\n")
cat("DatabasePath   :", db_path, "\n")
cat("TemplatesRoot  :", templates_root, "\n")
cat("Manifest       :", manifest_path, "\n\n")

# --- 1. Load manifest --------------------------------------------------------
if (!file.exists(manifest_path)) {
  stop("Templates manifest not found: ", manifest_path)
}
#manifest <- read.csv(manifest_path, stringsAsFactors = FALSE)
manifest <- read.csv(manifest_path)

print('----- Template manifest dataframe ----')
print(manifest)
print('--------------------------------------')


# required <- c("NodeID", "NodeType", "Title", "DocxFilename")
# missing  <- setdiff(required, names(manifest))
# if (length(missing) > 0) {
#   stop("Manifest is missing required columns: ", paste(missing, collapse = ", "))
# }

valid_types <- c("Document", "Section", "Subsection", "Clause", "HeaderNode", "Template")

# --- 2. Open DB connection ---------------------------------------------------
con <- dbConnect(SQLite(), db_path)
#on.exit(dbDisconnect(con), add = TRUE)

# --- 3. Loop over manifest rows ---------------------------------------------
inserted <- 0
skipped  <- 0
errors   <- 0

for (i in seq_len(nrow(manifest))) {
  row        <- manifest[i, ]
  node_id    <- trimws(row$NodeID)
  node_type  <- trimws(row$NodeType)
  node_title <- trimws(row$Title)
  docx_rel   <- trimws(row$DocxFilename)
  docx_path  <- file.path(templates_root, docx_rel)

  cat(sprintf("[%d/%d] %s  <-  %s\n", i, nrow(manifest), node_id, docx_rel))

  # Validate NodeType
  if (!(node_type %in% valid_types)) {
    cat("    SKIP: invalid NodeType '", node_type, "'\n", sep = "")
    errors <- errors + 1
    next
  }

  # Validate file exists and is readable
  if (!file.exists(docx_path)) {
    cat("    SKIP: file not found: ", docx_path, "\n", sep = "")
    errors <- errors + 1
    next
  }
  file_size <- file.info(docx_path)$size
  if (is.na(file_size) || file_size == 0) {
    cat("    SKIP: empty or unreadable file\n")
    errors <- errors + 1
    next
  }

  # Check for duplicate NodeID in DB
  existing <- dbGetQuery(con, "SELECT NodeID FROM Node WHERE NodeID = ?",
                        params = list(node_id))
  if (nrow(existing) > 0) {
    cat("    SKIP: NodeID already exists in Node table\n")
    skipped <- skipped + 1
    next
  }

  # Read DOCX as raw bytes and insert
  raw_bytes  <- readBin(docx_path, what = "raw", n = file_size)
  title_val  <- if (nchar(node_title) == 0) NA_character_ else node_title

  result <- tryCatch({
    dbExecute(
      con,
      "INSERT INTO Node (NodeID, NodeType, Title, Content) VALUES (?, ?, ?, ?)",
      params = list(node_id, node_type, title_val, list(raw_bytes))
    )
    TRUE
  }, error = function(e) {
    cat("    ERROR inserting ", node_id, ": ", conditionMessage(e), "\n", sep = "")
    FALSE
  })

  if (isTRUE(result)) {
    cat("    OK: inserted ", length(raw_bytes), " bytes\n", sep = "")
    inserted <- inserted + 1
  } else {
    errors <- errors + 1
  }
}

# --- 4. Summary + exit code --------------------------------------------------
cat("\n=== Summary ===\n")
cat("Inserted : ", inserted, "\n", sep = "")
cat("Skipped  : ", skipped,  " (already in DB)\n", sep = "")
cat("Errors   : ", errors,   "\n", sep = "")

if (errors > 0) {
  quit(status = 1, save = "no")
}
