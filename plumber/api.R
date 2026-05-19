# ── api.R ──────────────────────────────────────────────────────────────────────
#
# MAKEDOC Plumber API
#
# Endpoints:
#   GET  /health          — server and database status check
#   GET  /doctypes        — list all document types
#   GET  /instances       — list all active instances
#   POST /assemble        — assemble a document and return the DOCX bytes
#
# To start the server:
#   library(plumber)
#   pr <- plumb("api.R")
#   pr$run(host = "0.0.0.0", port = 8000)
#
# The MAKEDOC_DB environment variable must be set before starting.
#
# Required packages:
#   plumber, DBI, RSQLite, jsonlite, xml2, zip, uuid
# ──────────────────────────────────────────────────────────────────────────────

# Resolve the directory this file lives in. `source()` is CWD-relative,
# which breaks when plumber::plumb() is started from anywhere other than
# the plumber/ directory. Walk the frame stack looking for whichever
# frame has an `ofile` attribute set (set by source() / plumb()).
.api_dir <- local({
  for (n in seq_len(sys.nframe())) {
    f <- sys.frame(n)$ofile
    if (!is.null(f) && nzchar(f)) return(dirname(normalizePath(f)))
  }
  getwd()
})

source(file.path(.api_dir, "db.R"))
source(file.path(.api_dir, "assemble.R"))

library(uuid)


# ── Helpers ────────────────────────────────────────────────────────────────────

# Writes a JSON error body to res and returns res. Use this in any route
# whose declared @serializer is not JSON (e.g. /assemble which streams
# DOCX bytes on success) so error responses are still valid JSON.
error_response <- function(res, status, message) {
  res$status <- status
  res$setHeader("Content-Type", "application/json")
  res$body   <- jsonlite::toJSON(list(error = message), auto_unbox = TRUE)
  res
}

# Converts a data frame to a list of named lists, one per row. Safer
# than apply(df, 1, ...), which coerces every column to a common type.
rows_to_list <- function(df, columns) {
  lapply(seq_len(nrow(df)), function(i) {
    row  <- df[i, , drop = FALSE]
    vals <- lapply(columns, function(col) row[[col]])
    names(vals) <- names(columns)
    vals
  })
}


# ── GET /health ────────────────────────────────────────────────────────────────
#
# Returns server status and confirms the database is reachable.
# Use this to verify the server is running before making other calls.

#* @get /health
function(res) {

  db_status <- tryCatch({
    con <- open_connection()
    on.exit(dbDisconnect(con))
    count <- dbGetQuery(con, "SELECT COUNT(*) AS n FROM DocType")$n
    paste0("connected — ", count, " document type(s)")
  }, error = function(e) {
    res$status <- 503
    paste0("error: ", e$message)
  })

  list(
    status   = "ok",
    database = db_status,
    time     = format(Sys.time(), "%Y-%m-%dT%H:%M:%S")
  )
}


# ── GET /doctypes ──────────────────────────────────────────────────────────────
#
# Returns all document types as a JSON array.
# The WinForms AssemblyForm uses this to populate the DocType list.

#* @get /doctypes
function(res) {

  tryCatch({

    con <- open_connection()
    on.exit(dbDisconnect(con))

    rows <- get_all_doctypes(con)

    rows_to_list(rows, c(
      docTypeID      = "DocTypeID",
      name           = "Name",
      inclusionTags  = "InclusionTags",
      headerNodeID   = "HeaderNodeID",
      templateBlobID = "TemplateBlobID"
    ))

  }, error = function(e) {
    res$status <- 500
    list(error = e$message)
  })
}


# ── GET /instances ─────────────────────────────────────────────────────────────
#
# Returns all active (non-archived) instances as a JSON array.
# Includes the DocType name for display purposes.

#* @get /instances
function(res) {

  tryCatch({

    con <- open_connection()
    on.exit(dbDisconnect(con))

    rows <- get_active_instances(con)

    rows_to_list(rows, c(
      instanceID    = "InstanceID",
      docTypeID     = "DocTypeID",
      docTypeName   = "DocTypeName",
      generatedDate = "GeneratedDate",
      nodeList      = "NodeList"
    ))

  }, error = function(e) {
    res$status <- 500
    list(error = e$message)
  })
}


# ── POST /assemble ─────────────────────────────────────────────────────────────
#
# Assembles a document for the given DocTypeID.
#
# Request body (JSON):
#   { "docTypeId": "sol-micro" }
#
# On success:
#   - Returns the assembled DOCX as a binary stream
#   - Content-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document
#   - Creates an Instance record in the database
#
# On failure:
#   - Returns HTTP 400 or 500 with a JSON error body

#* @post /assemble
#* @serializer contentType list(type="application/vnd.openxmlformats-officedocument.wordprocessingml.document")
function(req, res) {

  # ── Parse request body ───────────────────────────────────────────────
  body <- tryCatch(
    jsonlite::fromJSON(req$postBody),
    error = function(e) NULL)

  if (is.null(body) || is.null(body$docTypeId))
    return(error_response(res, 400,
      "Request body must be JSON with a 'docTypeId' field."))

  doc_type_id <- trimws(body$docTypeId)

  tryCatch({

    con <- open_connection()
    on.exit(dbDisconnect(con))

    # ── Validate DocType ───────────────────────────────────────────────
    dt <- get_doctype(con, doc_type_id)

    if (nrow(dt) == 0)
      return(error_response(res, 400,
        paste0("DocType not found: ", doc_type_id)))

    header_node_id <- dt$HeaderNodeID[[1]]

    if (is.null(header_node_id) || is.na(header_node_id) || nchar(header_node_id) == 0)
      return(error_response(res, 400,
        paste0("DocType '", doc_type_id, "' has no HeaderNodeID configured.")))

    # ── Get ordered node list ──────────────────────────────────────────
    node_ids <- get_ordered_node_ids(con, doc_type_id, header_node_id)

    if (length(node_ids) == 0)
      return(error_response(res, 400,
        paste0("No nodes found in hierarchy for DocType: ", doc_type_id)))

    # ── Pull content blobs from Node table ────────────────────────────
    # Nodes with no Content blob (e.g. HeaderNode) are returned as NULL
    # and silently skipped by assemble_docx().
    blobs <- lapply(node_ids, function(id) get_node_content(con, id))

    # ── Assemble the document ──────────────────────────────────────────
    docx_bytes <- assemble_docx(blobs)

    # ── Insert Instance record ─────────────────────────────────────────
    instance_id <- UUIDgenerate()
    insert_instance(con, instance_id, doc_type_id, node_ids)

    # ── Return DOCX bytes ──────────────────────────────────────────────
    # The @serializer decorator above sets the Content-Type header.
    # Returning a raw vector causes Plumber to stream the bytes directly.
    docx_bytes

  }, error = function(e) {
    error_response(res, 500, e$message)
  })
}
