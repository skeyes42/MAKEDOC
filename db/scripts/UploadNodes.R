library(DBI)
library(RSQLite)
suppressPackageStartupMessages(
  suppressWarnings(
    library(readr)
  )
)
library(jsonlite)

# ── Configuration ──────────────────────────────────────────────────────────────
cfg <- fromJSON("C:/Users/skeye/BOOK2/MAKEDOC/config/makedoc.config.json")
CSV_PATH <- cfg$SeedPath
seed_file = paste0(CSV_PATH, '/', 'NodesToLoad.csv')
DOCS_ROOT <- cfg$ClausesRoot
DB_PATH <- cfg$DatabasePath

# ── Load CSV ───────────────────────────────────────────────────────────────────
nodes_to_load <- read_csv(seed_file,
                          col_names      = c("NodeID", "NodeType", "DocType"),
                          show_col_types = FALSE)

# Strip .docx from NodeID if it's already there
nodes_to_load$NodeID <- sub("\\.docx$", "", nodes_to_load$NodeID, ignore.case = TRUE)

cat(sprintf("Found %d nodes to load.\n", nrow(nodes_to_load)))


# ── Find all DOCX files under DOCS_ROOT ───────────────────────────────────────
all_files <- list.files(
  path        = DOCS_ROOT,
  pattern     = "\\.docx$",
  recursive   = TRUE,
  full.names  = TRUE,
  ignore.case = TRUE
)

cat(sprintf("Found %d DOCX files in folder.\n", length(all_files)))

# ── Connect to database ────────────────────────────────────────────────────────
con <- dbConnect(RSQLite::SQLite(), DB_PATH)

# ── Process each node ─────────────────────────────────────────────────────────
loaded    <- c()
not_found <- c()
errors    <- c()

cat(sprintf("\nCount of nodes to load: %d\n", nrow(nodes_to_load)))

for (i in seq_len(nrow(nodes_to_load))) {

  node_id   <- nodes_to_load$NodeID[i]
  docx_file <- paste0(node_id, ".docx")

  if(node_id == 'NL-0107') {
    print('here')
  }

  cat(sprintf("\nProcessing: %s\n", node_id))

  matches <- all_files[basename(all_files) == docx_file]

  if (length(matches) == 0) {
    cat(sprintf("  [NOT FOUND] %s\n", docx_file))
    not_found <- c(not_found, node_id)
    next
  }

  file_path <- matches[1]
  file_size <- file.info(file_path)$size
  raw_bytes <- readBin(file_path, what = "raw", n = file_size)

  cat(sprintf("  File: %s\n", file_path))
  cat(sprintf("  Read %d bytes\n", file_size))

  tryCatch({

    rows_affected <- dbExecute(con,
      "UPDATE Node SET Content = ? WHERE NodeID = ?",
      params = list(list(raw_bytes), node_id)
    )
    if (rows_affected == 0) {
      cat(sprintf("  [WARNING] No rows updated — NodeID not in Node table\n"))
      not_found <- c(not_found, node_id)
    } else {
      cat(sprintf("  [OK] Content saved\n"))
      loaded <- c(loaded, node_id)
    }
  }, error = function(e) {
    cat(sprintf("  [ERROR] %s\n", e$message))
    errors <- c(errors, node_id)
  })
}

# for (i in seq_len(nrow(nodes_to_load))) {

#   node_id   <- nodes_to_load$NodeID[i]
#   docx_file <- paste0(node_id, ".docx")

#   cat(sprintf("\nProcessing: %s\n", node_id))
  
#   # ── Step 1: Find the file ──────────────────────────────────────────────────

#   # Get the full paths of any files whose name matches e.g. "NL-0001.docx"
#   matches <- all_files[basename(all_files) == docx_file]

#   file_path <- matches[1]
#   cat(sprintf("  File: %s\n", file_path))

#   # ── Step 2: Read the file as binary ───────────────────────────────────────

#   file_size <- file.info(file_path)$size
#   raw_bytes <- readBin(file_path, what = "raw", n = file_size)
#   blob_data <- list(raw_bytes)   # RSQLite expects a list for BLOB columns

#   cat(sprintf("  Read %d bytes\n", file_size))

#   # ── Step 3: Insert into the Node table ────────────────────────────────────
#   # sql <- "UPDATE Node SET Content = ? WHERE NodeID = ?"

#   # dbExecute(con, sql, params = list(blob_data, node_id))


#   sql <- "INSERT INTO Node (NodeID, NodeType, Sequence, Content)
#         VALUES (?, ?, 0, ?)
#         ON CONFLICT(NodeID) DO UPDATE SET
#           Content = excluded.Content"

# dbExecute(con, sql, params = list(node_id, nodes_to_load$NodeType[i], blob_data))


# rows_affected <- dbExecute(con, sql, params = list(blob_data, node_id))
# if (rows_affected == 0) {
#   cat(sprintf("  [WARNING] No rows updated — NodeID not found in Node table\n"))
#   not_found <- c(not_found, node_id)
# } else {
#   cat(sprintf("  [OK] %d row(s) updated\n", rows_affected))
#   loaded <- c(loaded, node_id)
# }


# ── Disconnect ─────────────────────────────────────────────────────────────────
dbDisconnect(con)

# ── Summary ────────────────────────────────────────────────────────────────────
cat("\n──────────────────────────────────────────────────\n")
cat(sprintf("  Loaded:    %d\n", length(loaded)))
cat(sprintf("  Not found: %d\n", length(not_found)))
cat(sprintf("  Errors:    %d\n", length(errors)))

