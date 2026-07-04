# =============================================================================
# fillin_audit.R
# Fill-in variable audit for MAKEDOC clause library
#
# What this does:
#   1. Connects to the MAKEDOC SQLite database
#   2. Reads every Node's DOCX blob and extracts its text
#   3. Mines all {tag} patterns using a regular expression
#   4. Classifies each tag as canonical or anomalous
#   5. Cross-references tags with DocTypes via NodeHierarchy
#   6. Writes an HTML audit report
#
# Required packages: RSQLite, officer, DBI
# =============================================================================


# --- 0. Install packages if missing ------------------------------------------

required_packages <- c("RSQLite", "officer", "DBI")

# Find packages that aren't installed
missing_packages <- required_packages[
  !sapply(required_packages, requireNamespace, quietly = TRUE)
]

# Install any that are missing
if (length(missing_packages) > 0) {
  message("Installing: ", paste(missing_packages, collapse = ", "))
  install.packages(missing_packages, repos = "https://cloud.r-project.org")
}


library(DBI)
library(RSQLite)
library(officer)


# --- 1. Configuration --------------------------------------------------------

DB_PATH     <- "C:/Users/skeye/BOOK2/MAKEDOC/db/MAKEDOC.db"
OUT_REPORT  <- "fillin_audit.html"

# Tags considered correct. Everything else is anomalous.
CANONICAL_TAGS <- c("{SecNo}", "{lineitem}")


# --- 2. Connect to database and pull Node table ------------------------------
#
# RSQLite speaks directly to a SQLite file — no server, no ODBC driver.
# dbConnect() returns a connection object we pass to every query.

con <- dbConnect(RSQLite::SQLite(), DB_PATH)
on.exit(dbDisconnect(con), add = TRUE)   # always close, even on error

# Pull every node that has content (Content is stored as a BLOB = raw bytes)
nodes <- dbGetQuery(con, "
  SELECT NodeID, NodeType, Title, Content
  FROM   Node
  WHERE  Content IS NOT NULL
")

message("Nodes retrieved: ", nrow(nodes))

# Pull the DocType lookup and the NodeHierarchy join table
doctypes <- dbGetQuery(con, "SELECT DocTypeID, Type, Tier FROM DocType")
hierarchy <- dbGetQuery(con, "SELECT ParentNodeID, ChildNodeID, DocTypeID FROM NodeHierarchy")


# --- 3. Extract text from each DOCX blob ------------------------------------
#
# Each Content value arrives from RSQLite as a raw vector (the binary DOCX).
# officer::read_docx() normally reads a file path, but it also accepts a
# connection — so we wrap the raw bytes in rawConnection() first.
# docx_summary() returns a data frame; we keep only paragraph rows.

extract_text_from_blob <- function(blob) {
  tryCatch({
    # officer::read_docx() requires a real file path, not a connection.
    # Write the blob to a temp file, read it, then delete it.
    tmp <- tempfile(fileext = ".docx")
    on.exit(unlink(tmp), add = TRUE)
    writeBin(as.raw(blob), tmp)
    doc   <- read_docx(tmp)
    df    <- docx_summary(doc)
    paras <- df[df$content_type == "paragraph", "text", drop = TRUE]
    paste(paras, collapse = " ")
  }, error = function(e) {
    warning("Could not parse blob for a node: ", conditionMessage(e))
    ""
  })
}

message("Extracting text from ", nrow(nodes), " DOCX blobs ...")
nodes$text <- vapply(nodes$Content, extract_text_from_blob, character(1))


# --- 4. Mine {tag} patterns --------------------------------------------------
#
# The pattern \{[^}]+\} matches an opening brace, one or more non-} characters,
# then a closing brace.  regmatches() + gregexpr() returns all matches per string
# (gregexpr finds *all* occurrences, unlike regexpr which stops at the first).

FILL_PATTERN <- "\\{[^}]+\\}"

nodes$tags_found <- lapply(nodes$text, function(txt) {
  m <- regmatches(txt, gregexpr(FILL_PATTERN, txt))[[1]]
  if (length(m) == 0) character(0) else m
})

# Keep only nodes that contained at least one tag
has_tags <- vapply(nodes$tags_found, length, integer(1)) > 0
tagged   <- nodes[has_tags, ]

message("Nodes containing fill-in tags: ", nrow(tagged))


# --- 5. Classify tags: canonical vs anomalous --------------------------------

classify_tag <- function(tag) {
  if (tag %in% CANONICAL_TAGS)   return("canonical")
  if (grepl("secno", tag, ignore.case = TRUE)) return("bad_secno")
  if (grepl("lineitem", tag, ignore.case = TRUE)) return("lineitem_variant")
  return("unknown")
}

# Build a long-form data frame: one row per (NodeID, tag occurrence)
tag_rows <- do.call(rbind, lapply(seq_len(nrow(tagged)), function(i) {
  row   <- tagged[i, ]
  tags  <- unique(row$tags_found[[1]])          # deduplicate within node
  data.frame(
    NodeID   = row$NodeID,
    NodeType = row$NodeType,
    Title    = row$Title,
    Tag      = tags,
    Class    = vapply(tags, classify_tag, character(1)),
    stringsAsFactors = FALSE
  )
}))

# Attach DocType membership via NodeHierarchy
#   A node can appear in multiple DocTypes, so we collapse them into one string.
node_doctypes <- merge(
  hierarchy[, c("ChildNodeID", "DocTypeID")],
  doctypes,
  by = "DocTypeID"
)
node_doctypes$label <- paste0(node_doctypes$DocTypeID,
                               " (", node_doctypes$Tier, ")")

dt_summary <- aggregate(label ~ ChildNodeID,
                        data  = node_doctypes,
                        FUN   = function(x) paste(sort(unique(x)), collapse = ", "))
names(dt_summary) <- c("NodeID", "DocTypes")

tag_rows <- merge(tag_rows, dt_summary, by = "NodeID", all.x = TRUE)
tag_rows$DocTypes[is.na(tag_rows$DocTypes)] <- "(none)"

# Separate into subsets for the report
anomalous  <- tag_rows[tag_rows$Class != "canonical", ]
canonical  <- tag_rows[tag_rows$Class == "canonical", ]
bad_secno  <- tag_rows[tag_rows$Class == "bad_secno",  ]
li_variant <- tag_rows[tag_rows$Class == "lineitem_variant", ]


# --- 6. Summary statistics ---------------------------------------------------

all_unique_tags <- sort(unique(tag_rows$Tag))
n_nodes_total   <- nrow(tagged)
n_anomalous     <- length(unique(anomalous$NodeID))
n_bad_secno     <- length(unique(bad_secno$NodeID))
n_li_variant    <- length(unique(li_variant$NodeID))

cat("\n========== FILL-IN AUDIT SUMMARY ==========\n")
cat("Total nodes with tags    :", n_nodes_total, "\n")
cat("Unique tags found        :", length(all_unique_tags), "\n")
cat("  Canonical              :", length(CANONICAL_TAGS), "\n")
cat("  Anomalous              :", length(unique(anomalous$Tag)), "\n")
cat("Nodes with bad {SecNo}   :", n_bad_secno, "\n")
cat("Nodes with lineitem var  :", n_li_variant, "\n")
cat("============================================\n\n")


# --- 7. Build HTML report ----------------------------------------------------
#
# We build the HTML with paste() rather than a template engine to keep the
# script self-contained.  html_table() is a small helper that turns a
# data frame into an HTML <table>.

html_table <- function(df, id = "") {
  if (nrow(df) == 0) return("<p><em>None.</em></p>")

  header <- paste0("<th>", names(df), "</th>", collapse = "")
  rows   <- apply(df, 1, function(r) {
    cells <- paste0("<td>", r, "</td>", collapse = "")
    paste0("<tr>", cells, "</tr>")
  })
  paste0(
    '<table id="', id, '">',
    "<thead><tr>", header, "</tr></thead>",
    "<tbody>", paste(rows, collapse = ""), "</tbody>",
    "</table>"
  )
}

# --- lineitem table: tag, nodes that use it, DocTypes
# Guard against empty subset — aggregate() errors on zero rows
if (nrow(li_variant) > 0) {
  li_report <- aggregate(
    cbind(NodeCount = NodeID) ~ Tag + DocTypes,
    data = li_variant,
    FUN  = length
  )
  li_report <- li_report[order(li_report$Tag, li_report$DocTypes), ]
} else {
  li_report <- data.frame(Tag      = character(0),
                          DocTypes = character(0),
                          NodeCount = integer(0),
                          stringsAsFactors = FALSE)
}

# --- bad SecNo table
secno_report <- bad_secno[, c("NodeID", "Title", "Tag", "DocTypes")]
secno_report <- secno_report[order(secno_report$Tag, secno_report$NodeID), ]

# --- canonical usage summary: how many nodes use each canonical tag
canon_summary <- aggregate(NodeID ~ Tag, data = canonical, FUN = length)
names(canon_summary) <- c("Tag", "NodeCount")
canon_summary <- canon_summary[order(-canon_summary$NodeCount), ]

html <- paste0('<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>MAKEDOC Fill-in Audit</title>
  <style>
    body   { font-family: Segoe UI, Arial, sans-serif; margin: 40px; color: #222; }
    h1     { color: #003366; }
    h2     { color: #003366; border-bottom: 1px solid #ccc; padding-bottom: 4px; margin-top: 36px; }
    h3     { color: #555; }
    table  { border-collapse: collapse; width: 100%; margin-bottom: 24px; font-size: 0.9em; }
    th     { background: #003366; color: #fff; padding: 7px 10px; text-align: left; }
    td     { padding: 6px 10px; border-bottom: 1px solid #ddd; vertical-align: top; }
    tr:nth-child(even) td { background: #f5f8fc; }
    .badge-bad  { background: #c0392b; color: #fff; padding: 2px 7px; border-radius: 4px; font-size: 0.85em; }
    .badge-ok   { background: #27ae60; color: #fff; padding: 2px 7px; border-radius: 4px; font-size: 0.85em; }
    .badge-warn { background: #e67e22; color: #fff; padding: 2px 7px; border-radius: 4px; font-size: 0.85em; }
    .stat-box  { display:inline-block; background:#f0f4fa; border:1px solid #c5d3e8;
                 border-radius:6px; padding:14px 24px; margin:8px 8px 8px 0; }
    .stat-num  { font-size:2em; font-weight:bold; color:#003366; display:block; }
    .stat-lbl  { font-size:0.85em; color:#555; }
    pre        { background:#f4f4f4; padding:10px; border-radius:4px; font-size:0.88em; }
    .section-note { background:#fffbe6; border-left:4px solid #f0ad00;
                    padding:10px 16px; margin-bottom:16px; font-size:0.92em; }
  </style>
</head>
<body>

<h1>MAKEDOC &mdash; Fill-in Variable Audit</h1>
<p>Generated: ', format(Sys.time(), "%Y-%m-%d %H:%M"), ' &nbsp;|&nbsp;
   Database: <code>', DB_PATH, '</code></p>

<h2>Summary</h2>
<div>
  <div class="stat-box">
    <span class="stat-num">', n_nodes_total, '</span>
    <span class="stat-lbl">Nodes with tags</span>
  </div>
  <div class="stat-box">
    <span class="stat-num">', length(all_unique_tags), '</span>
    <span class="stat-lbl">Unique tag strings found</span>
  </div>
  <div class="stat-box">
    <span class="stat-num" style="color:#c0392b">', n_bad_secno, '</span>
    <span class="stat-lbl">Nodes with malformed {SecNo}</span>
  </div>
  <div class="stat-box">
    <span class="stat-num" style="color:#e67e22">', n_li_variant, '</span>
    <span class="stat-lbl">Nodes with lineitem variants</span>
  </div>
</div>

<h2>All Unique Tags Discovered</h2>
<p>Every distinct <code>{...}</code> string found across all nodes:</p>
<pre>', paste(all_unique_tags, collapse = "\n"), '</pre>

<h2>Canonical Tag Usage <span class="badge-ok">OK</span></h2>
<p>These tags match the declared canonical set and will substitute correctly.</p>
',
html_table(canon_summary, "tbl-canon"),
'
<h2>Malformed {SecNo} Tags <span class="badge-bad">ACTION REQUIRED</span></h2>
<div class="section-note">
  <strong>Problem:</strong> FillinService searches for the exact string
  <code>{SecNo}</code>. Any variation &mdash; wrong case, extra braces, stray
  spaces &mdash; causes a silent miss: the raw tag is printed in the assembled
  document instead of the section number.<br><br>
  <strong>Fix:</strong> In each affected node blob, replace the bad tag with
  <code>{SecNo}</code>. The three variants found are shown below.
</div>

<h3>Variants found</h3>
<table>
  <thead><tr><th>Bad tag</th><th>Likely cause</th><th>Affected nodes</th></tr></thead>
  <tbody>
    <tr><td><code>{{SecNo}</code></td>
        <td>Extra leading <code>{</code> &mdash; probably a copy/paste or template merge artifact</td>
        <td>', sum(bad_secno$Tag == "{{SecNo}"), '</td></tr>
    <tr><td><code>{ {SecNo}</code></td>
        <td>Stray space before the tag name</td>
        <td>', sum(bad_secno$Tag == "{ {SecNo}"), '</td></tr>
    <tr><td><code>{SECNO}</code></td>
        <td>All-caps &mdash; case mismatch with the substitution key</td>
        <td>', sum(bad_secno$Tag == "{SECNO}"), '</td></tr>
  </tbody>
</table>

<h3>Affected nodes (', nrow(secno_report), ' rows)</h3>
',
html_table(secno_report, "tbl-secno"),
'
<h2>Line Item Tag Variants <span class="badge-warn">REVIEW</span></h2>
<div class="section-note">
  Two spellings appear: <code>{lineitem}</code> (singular) and
  <code>{lineitems}</code> (plural). If FillinService treats them identically,
  one spelling should be chosen and enforced across all tiers. If they render
  differently (e.g., single row vs. full table), the distinction should be
  documented and verified for each document type.
</div>
',
html_table(li_report, "tbl-lineitem"),
'
<h2>Methodology</h2>
<p>This report was produced by <code>fillin_audit.R</code> using the following steps:</p>
<ol>
  <li>Connected to the SQLite database with <strong>RSQLite</strong>.</li>
  <li>Retrieved all Node rows whose <code>Content</code> column is non-null
      (stored as a DOCX binary blob).</li>
  <li>Wrapped each blob in <code>rawConnection()</code> and passed it to
      <strong>officer</strong><code>::read_docx()</code> to extract paragraph text.</li>
  <li>Applied the regular expression <code>\\{[^}]+\\}</code> with
      <code>gregexpr()</code> / <code>regmatches()</code> to find every
      <code>{...}</code> occurrence in each node.</li>
  <li>Classified each tag against the canonical set and grouped anomalies by type.</li>
  <li>Joined to <code>NodeHierarchy</code> and <code>DocType</code> to show
      which document types each affected node belongs to.</li>
</ol>

</body>
</html>')

writeLines(html, OUT_REPORT)
message("Report written to: ", normalizePath(OUT_REPORT))
