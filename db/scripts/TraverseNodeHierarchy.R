library(DBI)
library(RSQLite)
library(officer)
library(jsonlite)

cfg <- fromJSON("C:/Users/skeye/BOOK2/MAKEDOC/config/makedoc.config.json")
con <- NULL
con <- DBI::dbConnect(RSQLite::SQLite(), cfg$DatabasePath)

output_file <- "TraverseNodeHierarchy_output.txt"


tryCatch({

  sink(output_file, type = "output", split = TRUE)


get_first_docx_line <- function(content) {
  # Unwrap list wrapper (RSQLite returns blobs as lists)
  if (is.list(content)) {
    content <- content[[1]]
  }

  if (!is.raw(content)) {
    return(NA_character_)
  }

  tmp <- tempfile(fileext = ".docx")
  on.exit(unlink(tmp), add = TRUE)
  writeBin(content, tmp)

  tryCatch({
    doc        <- read_docx(tmp)
    content_df <- docx_summary(doc)
    paragraphs <- content_df[content_df$content_type == "paragraph", "text"]
    non_empty  <- paragraphs[nchar(trimws(paragraphs)) > 0]
    if (length(non_empty) == 0) return(NA_character_)
    first_ln <- substr(non_empty[1], 1, 50)
    return(first_ln)
    #return(trimws(non_empty[[1]]))
  }, error = function(e) {
    cat(sprintf("    [WARN] Could not read docx: %s\n", e$message))
    return(NA_character_)
  })
}


# Fetch node type lookup (lightweight — no blob column)
nodes       <- dbGetQuery(con, "SELECT NodeID, NodeType FROM Node")
node_lookup <- setNames(nodes$NodeType, nodes$NodeID)

# Fetch DocTypes
doctypes <- dbGetQuery(con, "SELECT DocTypeID, Name, HeaderNodeID FROM DocType")

# ── Helper: fetch a single node's content blob safely ─────────────────────────
get_node_content <- function(con, node_id) {
  row <- dbGetQuery(con,
    "SELECT Content FROM Node WHERE NodeID = ?",
    params = list(node_id)
  )
  if (nrow(row) == 0) return(NULL)
  row$Content[[1]]   # extract the single blob value
}

# ── Traverse hierarchy for one DocType ────────────────────────────────────────
traverse_hierarchy <- function(con, doctype_id, node_lookup) {
  rows <- dbGetQuery(con,
    "SELECT ParentNodeID, ChildNodeID, Sequence
     FROM NodeHierarchy
     WHERE DocTypeID = ?
     ORDER BY Sequence",
    params = list(doctype_id)
  )

  if (nrow(rows) == 0) {
    cat("  (no nodes found)\n")
    return(invisible(NULL))
  }

  head_node <- rows$ParentNodeID[1]
  next_node <- setNames(rows$ChildNodeID, rows$ParentNodeID)

  current <- head_node
  order   <- 1
  visited <- character(0)

  while (!is.na(current) && nchar(trimws(current)) > 0 && !(current %in% visited)) {
    node_type  <- node_lookup[current]
    content    <- get_node_content(con, current)
    first_line <- if (!is.null(content)) get_first_docx_line(content) else NA_character_

    cat(sprintf("  %2d. %-12s  %-20s  %s\n",
                order, current, node_type %||% "?", first_line %||% "(no text)"))

    visited <- c(visited, current)
    current <- next_node[current]
    order   <- order + 1
  }
}

# Simple null-coalescing helper
`%||%` <- function(a, b) if (!is.na(a) && !is.null(a)) a else b

# ── Main loop ─────────────────────────────────────────────────────────────────
for (i in seq_len(nrow(doctypes))) {
  cat(sprintf("\nDocType: %s (%s)\n", doctypes$DocTypeID[i], doctypes$Name[i]))
  cat(rep("-", 40), "\n", sep = "")
  traverse_hierarchy(con, doctypes$DocTypeID[i], node_lookup)
}


}, finally = {
  if (sink.number() > 0) sink(type = "output")
  if (!is.null(con)) dbDisconnect(con)

})