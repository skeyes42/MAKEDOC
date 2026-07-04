-- ── embedding_migrate.sql ─────────────────────────────────────────────────────
--
-- Adds the embedding layer tables to MAKEDOC.db.
--
--   NodeText       — plain text extracted from Node.Content (DOCX blob),
--                    cached so extraction happens once per content change
--   NodeEmbedding  — embedding vectors, one row per (node, chunk, model)
--
-- Staleness is detected by hash: a node's text or vector is stale when
-- sha256(Node.Content) no longer matches the stored ContentHash. Rows are
-- refreshed by POST /embeddings/refresh on the Plumber API.
--
-- Vectors are stored as BLOBs: float32[Dims], little-endian. Similarity is
-- computed in R (single matrix multiply) — no vector extension required at
-- current library scale. sqlite-vec is the upgrade path if that changes.
--
-- Apply with:
--   sqlite3 %MAKEDOC_DB%\MAKEDOC.db < embedding_migrate.sql
--
-- Idempotent: safe to run more than once.
-- ──────────────────────────────────────────────────────────────────────────────

PRAGMA foreign_keys = ON;

BEGIN TRANSACTION;

CREATE TABLE IF NOT EXISTS NodeText (
    NodeID      TEXT PRIMARY KEY REFERENCES Node(NodeID),
    PlainText   TEXT NOT NULL,
    ContentHash TEXT NOT NULL,                          -- sha256 hex of Node.Content
    ExtractedAt TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS NodeEmbedding (
    NodeID      TEXT    NOT NULL REFERENCES Node(NodeID),
    ChunkIndex  INTEGER NOT NULL DEFAULT 0,             -- clauses are short; almost always 0
    ChunkText   TEXT    NOT NULL,                       -- exact text that was embedded
    ContentHash TEXT    NOT NULL,                       -- NodeText.ContentHash at embed time
    Model       TEXT    NOT NULL,                       -- e.g. 'nomic-embed-text'
    Dims        INTEGER NOT NULL,                       -- vector length, e.g. 768
    Vector      BLOB    NOT NULL,                       -- float32[Dims], little-endian
    EmbeddedAt  TEXT    NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (NodeID, ChunkIndex, Model)
);

CREATE INDEX IF NOT EXISTS idx_nodeembedding_model
    ON NodeEmbedding(Model);

COMMIT;
