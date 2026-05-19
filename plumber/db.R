# ── db.R ───────────────────────────────────────────────────────────────────────
#
# SQLite helper functions for the MAKEDOC Plumber API.
#
# Covers:
#   - Opening a connection
#   - DocType queries
#   - Node content retrieval
#   - NodeHierarchy traversal  (replicates NodeHierarchyService.GetOrderedNodeIds)
#   - Instance insert
#   - Active instance retrieval
#
# All functions accept an open DBI connection as their first argument.
# The caller is responsible for opening and closing the connection.
# ──────────────────────────────────────────────────────────────────────────────

library(DBI)
library(RSQLite)
library(jsonlite)


# ── Connection ─────────────────────────────────────────────────────────────────

# Opens a connection to MAKEDOC.db.
# Reads the database directory from the MAKEDOC_DB environment variable,
# the same convention used by the C# application.
open_connection <- function() {

  db_dir <- Sys.getenv("MAKEDOC_DB")

  if (nchar(db_dir) == 0)
    stop("MAKEDOC_DB environment variable is not set.")

  db_path <- file.path(db_dir, "MAKEDOC.db")

  if (!file.exists(db_path))
    stop(paste("Database file not found:", db_path))

  dbConnect(RSQLite::SQLite(), db_path)
}


# ── DocType queries ────────────────────────────────────────────────────────────

# Returns all DocTypes as a data frame, ordered by Name.
get_all_doctypes <- function(con) {
  dbGetQuery(con,
    "SELECT DocTypeID, Name, InclusionTags, HeaderNodeID, TemplateBlobID
     FROM   DocType
     ORDER  BY Name")
}

# Returns a single DocType row, or an empty data frame if not found.
get_doctype <- function(con, doc_type_id) {
  dbGetQuery(con,
    "SELECT DocTypeID, Name, InclusionTags, HeaderNodeID, TemplateBlobID
     FROM   DocType
     WHERE  DocTypeID = ?",
    params = list(doc_type_id))
}


# ── Node content retrieval ─────────────────────────────────────────────────────

# Returns the raw BLOB content for a single Node, or NULL if not found.
# The Content column holds the DOCX file as a binary blob.
get_node_content <- function(con, node_id) {

  result <- dbGetQuery(con,
    "SELECT Content FROM Node WHERE NodeID = ?",
    params = list(node_id))

  if (nrow(result) == 0)
    return(NULL)

  blob <- result$Content[[1]]

  if (is.null(blob) || length(blob) == 0)
    return(NULL)

  # RSQLite returns BLOBs as raw vectors inside a list column.
  # Unwrap if necessary.
  if (is.list(blob)) blob[[1]] else blob
}


# ── NodeHierarchy traversal ────────────────────────────────────────────────────

# Returns a data frame of all NodeHierarchy edges for a given DocTypeID,
# ordered by Sequence.
get_hierarchy_edges <- function(con, doc_type_id) {
  dbGetQuery(con,
    "SELECT ParentNodeID, ChildNodeID, Sequence
     FROM   NodeHierarchy
     WHERE  DocTypeID = ?
     ORDER  BY Sequence",
    params = list(doc_type_id))
}

# Walks the linked-list chain starting from header_node_id and returns
# an ordered character vector of NodeIDs.
#
# This replicates NodeHierarchyService.GetOrderedNodeIds() from C#:
#   - Fetch all edges for the DocType
#   - Build a named lookup: ParentNodeID -> ChildNodeID
#   - Walk the chain until there is no next node or a cycle is detected
#
get_ordered_node_ids <- function(con, doc_type_id, header_node_id) {

  edges <- get_hierarchy_edges(con, doc_type_id)

  if (nrow(edges) == 0)
    return(character(0))

  # The linked-list model assumes each ParentNodeID has at most one
  # successor for a given DocType. If that invariant is ever broken in
  # the DB, the setNames() below would silently drop rows — surface it
  # instead.
  dups <- unique(edges$ParentNodeID[duplicated(edges$ParentNodeID)])
  if (length(dups) > 0)
    stop(sprintf(
      "NodeHierarchy for DocType '%s' has multiple successors for: %s",
      doc_type_id, paste(dups, collapse = ", ")))

  # Named vector: next_node["NL-0001"] == "NL-0002", etc.
  next_node <- setNames(edges$ChildNodeID, edges$ParentNodeID)

  ordered <- character(0)
  visited <- character(0)
  current <- header_node_id

  while (!is.null(current) &&
         nchar(current) > 0 &&
         !(current %in% visited)) {

    ordered <- c(ordered, current)
    visited <- c(visited, current)

    # Look up the next node; returns NA if current is not a key in next_node
    nxt <- unname(next_node[current])
    current <- if (is.na(nxt)) NULL else nxt
  }

  ordered
}


# ── Instance insert ────────────────────────────────────────────────────────────

# Inserts a new Instance record into the database.
# GeneratedDate and IsArchived are handled by SQLite defaults.
#
# node_ids  — character vector of NodeIDs in assembly order
#             stored as a JSON array: ["NL-0001", "NL-0014", ...]
#
insert_instance <- function(con, instance_id, doc_type_id, node_ids) {

  node_list_json <- toJSON(node_ids, auto_unbox = FALSE)

  dbExecute(con,
    "INSERT INTO Instance (InstanceID, DocTypeID, NodeList)
     VALUES (?, ?, ?)",
    params = list(instance_id, doc_type_id, node_list_json))
}


# ── Instance queries ───────────────────────────────────────────────────────────

# Returns all active (non-archived) instances joined to DocType for display.
get_active_instances <- function(con) {
  dbGetQuery(con,
    "SELECT i.InstanceID,
            i.DocTypeID,
            d.Name        AS DocTypeName,
            i.GeneratedDate,
            i.NodeList
     FROM   Instance i
     LEFT   JOIN DocType d ON i.DocTypeID = d.DocTypeID
     WHERE  i.IsArchived = 0
     ORDER  BY i.GeneratedDate DESC")
}
