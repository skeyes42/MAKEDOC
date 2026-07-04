# Embedding Layer — Design Sketch

Semantic clause search, near-duplicate detection, and embedding-based clustering
for the MAKEDOC clause library. Sits between FTS5 (exact search) and any future
generative endpoints. Runs entirely local: Ollama serves the embedder, vectors
live in `MAKEDOC.db`, Plumber exposes the API.

```
MakeDoc.App / MakeDoc.PlumberClient
        │  HTTP
        ▼
Plumber (api.R + embed.R)  ──HTTP──▶  Ollama  /api/embed  (nomic-embed-text)
        │
        ▼
MAKEDOC.db  (Node, NodeText, NodeEmbedding)
```

## Model choice

`nomic-embed-text` via Ollama: 768 dimensions, ~270 MB, CPU-friendly, strong on
retrieval benchmarks. One quirk that matters: it is trained with task prefixes.
Prepend `search_document: ` when embedding clauses and `search_query: ` when
embedding a user query. Skipping the prefixes silently degrades ranking.

Alternative: `bge-m3` (1024 dims, better multilingual) — the schema below is
model-agnostic, so switching is a re-embed, not a redesign.

## Schema

Two new tables. `NodeText` caches plain text extracted from the DOCX blob so
extraction (unzip + parse `word/document.xml`) happens once, not on every
re-embed or snippet display. `NodeEmbedding` holds the vectors.

```sql
-- Plain text extracted from Node.Content (w:t runs, joined per paragraph)
CREATE TABLE NodeText (
    NodeID      TEXT PRIMARY KEY REFERENCES Node(NodeID),
    PlainText   TEXT NOT NULL,
    ContentHash TEXT NOT NULL,              -- SHA-256 of Node.Content blob
    ExtractedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE NodeEmbedding (
    NodeID      TEXT NOT NULL REFERENCES Node(NodeID),
    ChunkIndex  INTEGER NOT NULL DEFAULT 0, -- clauses are short; almost always 0
    ChunkText   TEXT NOT NULL,              -- exact text that was embedded
    ContentHash TEXT NOT NULL,              -- copy of NodeText.ContentHash at embed time
    Model       TEXT NOT NULL,              -- 'nomic-embed-text'
    Dims        INTEGER NOT NULL,           -- 768
    Vector      BLOB NOT NULL,              -- float32[Dims], little-endian
    EmbeddedAt  TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (NodeID, ChunkIndex, Model)
);

CREATE INDEX idx_nodeembedding_model ON NodeEmbedding(Model);
```

Design notes:

- **Staleness by hash, not date.** A node is stale when
  `sha256(Node.Content) != NodeEmbedding.ContentHash`. Editing a clause
  invalidates its vector automatically; nothing else needs to know.
- **Vector as BLOB, cosine in R.** At clause-library scale (hundreds to a few
  thousand nodes), similarity is one matrix multiply against an in-memory
  float matrix — milliseconds, no index needed. `sqlite-vec` (a `vec0` virtual
  table) is the upgrade path if the corpus grows to tens of thousands; the C#
  side can load it trivially via `SqliteConnection.LoadExtension`, R less so.
  Don't buy that complexity yet.
- **Model in the primary key** lets two embedders coexist during an A/B
  comparison or migration.
- **Chunking:** embed whole clauses. Only split (by paragraph, ~1500-word
  target) if a clause exceeds the model's context (~8k tokens for nomic) —
  rare for procurement clauses. `ChunkIndex` exists so the schema doesn't
  change when it happens.
- **Instances** are deliberately not embedded — they are concatenations of
  nodes, so node-level vectors plus `Instance.NodeList` already cover
  "find instances containing clauses like X".

## Ollama call

One endpoint does everything. Batch inputs — one HTTP call per ~64 clauses,
not per clause.

```
POST http://127.0.0.1:11434/api/embed
{
  "model": "nomic-embed-text",
  "input": ["search_document: <clause text 1>", "search_document: <clause text 2>", ...]
}
→ { "embeddings": [[0.011, -0.024, ...], ...] }
```

## Plumber endpoints (module: plumber/embed.R, routes in api.R)

Follows the existing api.R conventions: logic in a sourced module, route
annotations in api.R, `open_connection()` from db.R. Ollama-connectivity
errors map to HTTP 503 so FTS5 search degrades cleanly when Ollama is down.

```
GET  /embeddings/status                     — model, embedded count, stale count
POST /embeddings/refresh                    — extract text + (re)embed stale/missing nodes
GET  /search/semantic?q=&k=10               — embed query, cosine top-k over library
GET  /embeddings/similar/<id>?k=10          — nearest neighbors of an existing node
GET  /embeddings/duplicates?threshold=0.92  — all pairs above cosine threshold
GET  /embeddings/clusters?k=8               — k-means on the embedding matrix
```

Implemented in `plumber/embed.R` (logic) and `plumber/api.R` (routes);
schema migration in `db/sql/embedding_migrate.sql`.

- `/embeddings/refresh` is the only writer. Idempotent: hash-compare, skip
  fresh rows, embed the rest, upsert. Returns `{scanned, skipped, embedded}`.
- `/search/semantic` returns `[{nodeId, title, score, snippet}]` — same shape
  an FTS5 search endpoint would return, so the C# UI can offer "exact" vs
  "semantic" as a toggle on one search box.
- `/nodes/duplicates` is the clause-library hygiene tool: near-identical
  clauses that drifted apart (a `UC-` user clause pasted from an `NL-` library
  clause, lightly edited). Threshold ~0.92 for nomic is a starting point;
  calibrate against known pairs.
- `/clusters/semantic` supersedes the TF-IDF path in `cluster_docs.R`; same
  downstream plotting (dendrogram/heatmap) with a better distance matrix.

## R implementation sketch (plumber/embed.R)

```r
library(httr2)
library(openssl)   # sha256

OLLAMA_URL  <- Sys.getenv("OLLAMA_URL", "http://127.0.0.1:11434")
EMBED_MODEL <- "nomic-embed-text"

# ── Ollama ────────────────────────────────────────────────────────────
ollama_embed <- function(texts, prefix = "search_document: ") {
  resp <- request(paste0(OLLAMA_URL, "/api/embed")) |>
    req_body_json(list(model = EMBED_MODEL, input = paste0(prefix, texts))) |>
    req_perform() |> resp_body_json()
  # list of numeric vectors -> matrix, one row per input
  do.call(rbind, lapply(resp$embeddings, unlist))
}

# ── BLOB round-trip (float32, little-endian) ──────────────────────────
vec_to_blob <- function(v) writeBin(as.numeric(v), raw(), size = 4, endian = "little")
blob_to_vec <- function(b) readBin(b, "numeric", n = length(b) / 4, size = 4, endian = "little")

# ── Load library matrix once per request (cache if it matters) ───────
load_matrix <- function(con, model = EMBED_MODEL) {
  df <- dbGetQuery(con,
    "SELECT NodeID, Vector FROM NodeEmbedding WHERE Model = ?", params = list(model))
  m <- do.call(rbind, lapply(df$Vector, blob_to_vec))
  rownames(m) <- df$NodeID
  m
}

# ── Cosine top-k: normalize rows, then it's a single %*% ─────────────
cosine_topk <- function(m, q, k = 10) {
  mn <- m / sqrt(rowSums(m^2)); qn <- q / sqrt(sum(q^2))
  s  <- as.vector(mn %*% qn)
  o  <- order(s, decreasing = TRUE)[seq_len(min(k, length(s)))]
  data.frame(NodeID = rownames(m)[o], Score = s[o])
}

#* @get /search/semantic
function(q = "", k = 10, res) {
  if (!nzchar(q)) return(error_response(res, 400, "q is required"))
  con <- open_connection(); on.exit(dbDisconnect(con))
  qv  <- ollama_embed(q, prefix = "search_query: ")[1, ]
  cosine_topk(load_matrix(con), qv, as.integer(k))
  # join Title + snippet from Node/NodeText before returning
}
```

Text extraction for `/embeddings/refresh` reuses the machinery already in
`assemble.R`: unzip `Node.Content` in memory (`zip`), read `word/document.xml`
(`xml2`), collect `//w:t` text grouped by `w:p`. Hash the raw blob, not the
extracted text, so any content change — including formatting-only — triggers
re-embed (cheap and simple beats clever here).

## C# side

No schema knowledge needed in the app. Add three thin methods to
`MakeDoc.PlumberClient` mirroring the endpoints (`SearchSemanticAsync`,
`GetSimilarNodesAsync`, `RefreshEmbeddingsAsync`). The Analytics form gains a
semantic search box and a "Find duplicate clauses" report; the existing clause
picker could later call `/nodes/<id>/similar` for a "clauses like this one"
panel.

## Ops notes

- **Prereqs:** `ollama pull nomic-embed-text`; R packages `httr2`, `openssl`
  (DBI/RSQLite/jsonlite/xml2/zip already required by api.R).
- **Rebuild script:** add a `POST /embeddings/refresh` call (or an Rscript
  equivalent) as the last step of `REBUILD_MAKEDOC_DATABASE.ps1`.
- **Degradation:** if Ollama is down, `/search/semantic` should 503 with a
  clear message; FTS5 search still works. Health check: `GET /api/tags` on
  Ollama from `/health`.
- **First full embed** of a few thousand clauses on CPU is minutes, not hours;
  incremental refreshes after that are near-instant thanks to hash skipping.
