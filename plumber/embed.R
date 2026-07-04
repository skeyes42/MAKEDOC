# ── embed.R ────────────────────────────────────────────────────────────────────
#
# Embedding layer for the MAKEDOC Plumber API.
#
# Covers:
#   - Plain-text extraction from Node.Content DOCX blobs (cached in NodeText)
#   - Embedding via a local Ollama server (POST /api/embed)
#   - Vector storage as float32 BLOBs in NodeEmbedding
#   - Cosine similarity: search, nearest-neighbor, duplicate detection
#   - K-means clustering over the embedding matrix
#
# Route annotations live in api.R (same pattern as assemble.R / db.R).
# All db functions accept an open DBI connection; the caller owns it.
#
# Configuration (environment variables):
#   OLLAMA_URL           — default http://127.0.0.1:11434
#   MAKEDOC_EMBED_MODEL  — default nomic-embed-text
#
# nomic-embed-text is trained with task prefixes. Clauses are embedded with
# "search_document: ", queries with "search_query: ". Omitting the prefixes
# silently degrades ranking quality — do not remove them.
#
# Schema: db/sql/embedding_migrate.sql
# Design: docs/features/embedding-layer-design.md
#
# Packages required (beyond api.R's base set):
#   httr2   — Ollama HTTP calls
#   openssl — sha256 content hashing
# ──────────────────────────────────────────────────────────────────────────────

library(httr2)
library(openssl)

OLLAMA_URL  <- Sys.getenv("OLLAMA_URL", "http://127.0.0.1:11434")
EMBED_MODEL <- Sys.getenv("MAKEDOC_EMBED_MODEL", "nomic-embed-text")

# Batch size for /api/embed calls. One HTTP round trip per batch.
EMBED_BATCH_SIZE <- 32

# Chunking threshold. nomic-embed-text has an ~8k-token context; procurement
# clauses almost never approach it. Split only past ~24k characters.
CHUNK_MAX_CHARS <- 24000


# ── Ollama client ──────────────────────────────────────────────────────────────

# TRUE if the Ollama server responds; used by /health and /embeddings/status.
ollama_reachable <- function() {
  tryCatch({
    request(paste0(OLLAMA_URL, "/api/tags")) |>
      req_timeout(5) |>
      req_perform()
    TRUE
  }, error = function(e) FALSE)
}

# Embeds a character vector of texts. Returns a numeric matrix, one row per
# input text. `prefix` is the nomic task prefix (see header comment).
#
# Errors from an unreachable server propagate with a message containing
# "Ollama" so routes can map them to HTTP 503.
ollama_embed <- function(texts, prefix = "search_document: ") {

  if (length(texts) == 0)
    return(matrix(numeric(0), nrow = 0, ncol = 0))

  batches <- split(texts, ceiling(seq_along(texts) / EMBED_BATCH_SIZE))

  rows <- lapply(batches, function(batch) {

    resp <- tryCatch({
      request(paste0(OLLAMA_URL, "/api/embed")) |>
        req_body_json(list(
          model = EMBED_MODEL,
          input = as.list(paste0(prefix, batch)))) |>
        req_timeout(300) |>
        req_perform() |>
        resp_body_json()
    }, error = function(e) {
      stop(sprintf("Ollama embed call failed (%s, model %s): %s",
                   OLLAMA_URL, EMBED_MODEL, conditionMessage(e)))
    })

    if (is.null(resp$embeddings) || length(resp$embeddings) != length(batch))
      stop("Ollama returned an unexpected embedding count.")

    do.call(rbind, lapply(resp$embeddings, function(v) as.numeric(unlist(v))))
  })

  do.call(rbind, rows)
}


# ── Vector BLOB round-trip ─────────────────────────────────────────────────────
#
# float32, little-endian. writeBin with size = 4 performs the double -> float
# narrowing; readBin reverses it. Both sides must agree on endianness — the
# C# reader (BinaryPrimitives / BitConverter on x86-64) is little-endian too.

vec_to_blob <- function(v) {
  writeBin(as.numeric(v), raw(), size = 4, endian = "little")
}

blob_to_vec <- function(b) {
  readBin(b, what = "numeric", n = length(b) / 4, size = 4, endian = "little")
}


# ── Plain-text extraction ──────────────────────────────────────────────────────

# Extracts plain text from a DOCX blob (raw vector). Returns a single string:
# paragraphs joined with newlines, empty paragraphs dropped. Returns "" if the
# blob has no document.xml or no text.
#
# Only w:t runs are collected (same text Word displays), grouped per w:p so
# paragraph boundaries survive.
extract_plain_text <- function(blob) {

  docx_path <- tempfile(fileext = ".docx")
  writeBin(blob, docx_path)
  on.exit(if (file.exists(docx_path)) file.remove(docx_path), add = TRUE)

  extract_dir <- tempfile()
  dir.create(extract_dir)
  on.exit(unlink(extract_dir, recursive = TRUE), add = TRUE)

  ok <- tryCatch({
    unzip(docx_path, files = "word/document.xml", exdir = extract_dir)
    TRUE
  }, warning = function(w) FALSE, error = function(e) FALSE)

  xml_path <- file.path(extract_dir, "word", "document.xml")
  if (!ok || !file.exists(xml_path))
    return("")

  ns    <- c(w = W_NS)   # from assemble.R
  doc   <- read_xml(xml_path)
  paras <- xml_find_all(doc, ".//w:p", ns)

  if (length(paras) == 0)
    return("")

  para_text <- vapply(paras, function(p) {
    paste(xml_text(xml_find_all(p, ".//w:t", ns)), collapse = "")
  }, character(1))

  para_text <- trimws(para_text)
  paste(para_text[nzchar(para_text)], collapse = "\n")
}

# sha256 hex digest of a raw vector (the Node.Content blob).
content_hash <- function(blob) {
  as.character(openssl::sha256(blob))
}

# Splits text into chunks of at most CHUNK_MAX_CHARS, breaking on paragraph
# boundaries. Nearly always returns the input unchanged as a single chunk.
chunk_text <- function(text) {

  if (nchar(text) <= CHUNK_MAX_CHARS)
    return(text)

  paras  <- strsplit(text, "\n", fixed = TRUE)[[1]]
  chunks <- character(0)
  buf    <- ""

  for (p in paras) {
    candidate <- if (nzchar(buf)) paste(buf, p, sep = "\n") else p
    if (nchar(candidate) > CHUNK_MAX_CHARS && nzchar(buf)) {
      chunks <- c(chunks, buf)
      buf    <- p
    } else {
      buf <- candidate
    }
  }

  c(chunks, buf)
}


# ── Refresh pipeline ───────────────────────────────────────────────────────────

# Re-extracts text and re-embeds every node whose Content blob has changed
# since it was last processed (hash comparison), plus any node never seen.
# Idempotent; fresh rows are skipped. The only writer in this module.
#
# Returns list(scanned, textRefreshed, embedded, skipped, emptied).
refresh_embeddings <- function(con) {

  nodes <- dbGetQuery(con,
    "SELECT NodeID, Content FROM Node WHERE Content IS NOT NULL")

  text_hashes <- dbGetQuery(con,
    "SELECT NodeID, ContentHash FROM NodeText")
  text_hash <- setNames(text_hashes$ContentHash, text_hashes$NodeID)

  embed_hashes <- dbGetQuery(con,
    "SELECT DISTINCT NodeID, ContentHash FROM NodeEmbedding WHERE Model = ?",
    params = list(EMBED_MODEL))
  embed_hash <- setNames(embed_hashes$ContentHash, embed_hashes$NodeID)

  n_text <- 0L; n_embed <- 0L; n_skip <- 0L; n_empty <- 0L

  # Nodes needing embedding are accumulated and sent to Ollama in batches.
  pending_ids    <- character(0)
  pending_chunks <- list()   # per node: character vector of chunk texts
  pending_hashes <- character(0)

  for (i in seq_len(nrow(nodes))) {

    node_id <- nodes$NodeID[[i]]
    blob    <- nodes$Content[[i]]
    if (is.list(blob)) blob <- blob[[1]]
    if (is.null(blob) || length(blob) == 0) next

    hash <- content_hash(blob)

    # ── Text extraction (cache in NodeText) ─────────────────────────────
    if (is.na(text_hash[node_id]) || text_hash[node_id] != hash) {
      plain <- extract_plain_text(blob)
      dbExecute(con,
        "INSERT INTO NodeText (NodeID, PlainText, ContentHash, ExtractedAt)
         VALUES (?, ?, ?, datetime('now'))
         ON CONFLICT(NodeID) DO UPDATE SET
           PlainText   = excluded.PlainText,
           ContentHash = excluded.ContentHash,
           ExtractedAt = excluded.ExtractedAt",
        params = list(node_id, plain, hash))
      n_text <- n_text + 1L
    } else {
      plain <- NULL   # fetched below only if embedding is needed
    }

    # ── Embedding staleness check ───────────────────────────────────────
    if (!is.na(embed_hash[node_id]) && embed_hash[node_id] == hash) {
      n_skip <- n_skip + 1L
      next
    }

    if (is.null(plain)) {
      cached <- dbGetQuery(con,
        "SELECT PlainText FROM NodeText WHERE NodeID = ?",
        params = list(node_id))
      plain <- if (nrow(cached) > 0) cached$PlainText[[1]] else ""
    }

    if (is.null(plain) || !nzchar(trimws(plain))) {
      # Nothing embeddable (e.g. image-only clause) — drop stale vectors.
      dbExecute(con,
        "DELETE FROM NodeEmbedding WHERE NodeID = ? AND Model = ?",
        params = list(node_id, EMBED_MODEL))
      n_empty <- n_empty + 1L
      next
    }

    pending_ids    <- c(pending_ids, node_id)
    pending_chunks <- c(pending_chunks, list(chunk_text(plain)))
    pending_hashes <- c(pending_hashes, hash)
  }

  # ── Embed pending nodes ──────────────────────────────────────────────
  if (length(pending_ids) > 0) {

    flat_texts <- unlist(pending_chunks)
    vectors    <- ollama_embed(flat_texts, prefix = "search_document: ")
    dims       <- ncol(vectors)

    row <- 1L
    dbExecute(con, "BEGIN TRANSACTION")
    ok <- tryCatch({

      for (j in seq_along(pending_ids)) {

        node_id <- pending_ids[[j]]
        chunks  <- pending_chunks[[j]]

        # Delete-then-insert so a shrinking chunk count leaves no orphans.
        dbExecute(con,
          "DELETE FROM NodeEmbedding WHERE NodeID = ? AND Model = ?",
          params = list(node_id, EMBED_MODEL))

        for (ci in seq_along(chunks)) {
          dbExecute(con,
            "INSERT INTO NodeEmbedding
               (NodeID, ChunkIndex, ChunkText, ContentHash,
                Model, Dims, Vector, EmbeddedAt)
             VALUES (?, ?, ?, ?, ?, ?, ?, datetime('now'))",
            params = list(
              node_id, ci - 1L, chunks[[ci]], pending_hashes[[j]],
              EMBED_MODEL, dims, list(vec_to_blob(vectors[row, ]))))
          row <- row + 1L
        }
        n_embed <- n_embed + 1L
      }

      dbExecute(con, "COMMIT")
      TRUE
    }, error = function(e) {
      dbExecute(con, "ROLLBACK")
      stop(e)
    })
  }

  list(
    scanned       = nrow(nodes),
    textRefreshed = n_text,
    embedded      = n_embed,
    skipped       = n_skip,
    emptied       = n_empty
  )
}


# ── Similarity ─────────────────────────────────────────────────────────────────

# Loads the full embedding matrix for a model: one row per (NodeID, ChunkIndex),
# rownames = NodeID (chunk rows share the node's ID; top-k dedupes by node).
# Returns NULL when no embeddings exist.
load_embedding_matrix <- function(con, model = EMBED_MODEL) {

  df <- dbGetQuery(con,
    "SELECT NodeID, Vector FROM NodeEmbedding WHERE Model = ?
     ORDER BY NodeID, ChunkIndex",
    params = list(model))

  if (nrow(df) == 0)
    return(NULL)

  m <- do.call(rbind, lapply(df$Vector, function(b) {
    if (is.list(b)) b <- b[[1]]
    blob_to_vec(b)
  }))
  rownames(m) <- df$NodeID
  m
}

# L2-normalizes matrix rows so cosine similarity becomes a plain dot product.
normalize_rows <- function(m) {
  m / sqrt(rowSums(m^2))
}

# Top-k rows of `m` most cosine-similar to query vector `q`, deduplicated by
# NodeID (a multi-chunk node scores by its best chunk). Returns a data frame
# (NodeID, Score) sorted by descending score.
cosine_topk <- function(m, q, k = 10) {

  mn <- normalize_rows(m)
  qn <- q / sqrt(sum(q^2))
  s  <- as.vector(mn %*% qn)

  df <- data.frame(NodeID = rownames(m), Score = s,
                   stringsAsFactors = FALSE)
  df <- df[order(df$Score, decreasing = TRUE), ]
  df <- df[!duplicated(df$NodeID), ]

  head(df, n = as.integer(k))
}

# Joins Title (Node) and a snippet (NodeText) onto a (NodeID, Score) frame,
# returning the list-of-lists shape the API serializes.
similarity_results <- function(con, df, snippet_chars = 200) {

  lapply(seq_len(nrow(df)), function(i) {

    node_id <- df$NodeID[[i]]

    meta <- dbGetQuery(con,
      "SELECT n.Title, substr(t.PlainText, 1, ?) AS Snippet
       FROM   Node n
       LEFT   JOIN NodeText t ON t.NodeID = n.NodeID
       WHERE  n.NodeID = ?",
      params = list(snippet_chars, node_id))

    list(
      nodeId  = node_id,
      title   = if (nrow(meta) > 0) meta$Title[[1]] else NA,
      score   = round(df$Score[[i]], 4),
      snippet = if (nrow(meta) > 0) meta$Snippet[[1]] else NA
    )
  })
}

# All node pairs with cosine similarity >= threshold — the clause-library
# hygiene report. Multi-chunk nodes pair by their best-scoring chunks.
find_duplicates <- function(con, threshold = 0.92) {

  m <- load_embedding_matrix(con)
  if (is.null(m) || nrow(m) < 2)
    return(list())

  mn <- normalize_rows(m)
  s  <- mn %*% t(mn)

  hits <- which(upper.tri(s) & s >= threshold, arr.ind = TRUE)
  if (nrow(hits) == 0)
    return(list())

  ids   <- rownames(m)
  pairs <- data.frame(
    A     = ids[hits[, "row"]],
    B     = ids[hits[, "col"]],
    Score = s[hits],
    stringsAsFactors = FALSE)

  pairs <- pairs[pairs$A != pairs$B, ]                    # cross-chunk self-pairs

  # Best score per unordered pair: sort descending, keep first occurrence.
  # key must be reordered in lockstep with pairs or duplicated() misaligns.
  key   <- ifelse(pairs$A < pairs$B,
                  paste(pairs$A, pairs$B),
                  paste(pairs$B, pairs$A))
  ord   <- order(pairs$Score, decreasing = TRUE)
  pairs <- pairs[ord, ]
  key   <- key[ord]
  pairs <- pairs[!duplicated(key), ]

  titles <- dbGetQuery(con, "SELECT NodeID, Title FROM Node")
  title  <- setNames(titles$Title, titles$NodeID)

  lapply(seq_len(nrow(pairs)), function(i) {
    list(
      nodeA  = pairs$A[[i]],
      titleA = unname(title[pairs$A[[i]]]),
      nodeB  = pairs$B[[i]],
      titleB = unname(title[pairs$B[[i]]]),
      score  = round(pairs$Score[[i]], 4)
    )
  })
}

# K-means over the (chunk-0) embedding matrix. Seeded for reproducibility.
# Returns list(k, clusters = [{nodeId, title, cluster}]).
cluster_embeddings <- function(con, k = 8) {

  df <- dbGetQuery(con,
    "SELECT e.NodeID, e.Vector, n.Title
     FROM   NodeEmbedding e
     JOIN   Node n ON n.NodeID = e.NodeID
     WHERE  e.Model = ? AND e.ChunkIndex = 0",
    params = list(EMBED_MODEL))

  if (nrow(df) == 0)
    stop("No embeddings found — run POST /embeddings/refresh first.")

  k <- min(as.integer(k), nrow(df))

  m <- do.call(rbind, lapply(df$Vector, function(b) {
    if (is.list(b)) b <- b[[1]]
    blob_to_vec(b)
  }))
  mn <- normalize_rows(m)   # unit vectors -> spherical k-means, effectively

  set.seed(42)
  km <- kmeans(mn, centers = k, nstart = 10)

  list(
    k        = k,
    clusters = lapply(seq_len(nrow(df)), function(i) {
      list(
        nodeId  = df$NodeID[[i]],
        title   = df$Title[[i]],
        cluster = unname(km$cluster[[i]])
      )
    })
  )
}


# ── Status ─────────────────────────────────────────────────────────────────────

embedding_status <- function(con) {

  counts <- dbGetQuery(con, "
    SELECT
      (SELECT COUNT(*) FROM Node WHERE Content IS NOT NULL)     AS nodesWithContent,
      (SELECT COUNT(*) FROM NodeText)                           AS textExtracted,
      (SELECT COUNT(DISTINCT NodeID) FROM NodeEmbedding
        WHERE Model = ?)                                        AS embedded,
      (SELECT COUNT(DISTINCT e.NodeID)
         FROM NodeEmbedding e
         JOIN NodeText t ON t.NodeID = e.NodeID
        WHERE e.Model = ? AND e.ContentHash <> t.ContentHash)   AS staleEmbeddings",
    params = list(EMBED_MODEL, EMBED_MODEL))

  list(
    model           = EMBED_MODEL,
    ollamaUrl       = OLLAMA_URL,
    ollamaReachable = ollama_reachable(),
    nodesWithContent = counts$nodesWithContent[[1]],
    textExtracted    = counts$textExtracted[[1]],
    embedded         = counts$embedded[[1]],
    staleEmbeddings  = counts$staleEmbeddings[[1]]
  )
}
