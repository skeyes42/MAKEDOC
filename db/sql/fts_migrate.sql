-- =============================================
-- FTS5 Migration for MAKEDOC
-- Adds plain-text extraction column to Node,
-- creates the FTS5 virtual table, and wires up
-- triggers to keep the index in sync.
--
-- Run once against MAKEDOC.db:
--   sqlite3 path\to\MAKEDOC.db < fts_migrate.sql
-- =============================================

PRAGMA foreign_keys = ON;


-- ── 1. FTS5 virtual table (standalone) ───────────────────────────────────────
-- Standalone (not external-content) for SQLite compatibility with large BLOBs.
-- Text is stored in both Node.PlainText and the FTS index; the triggers below
-- keep them in sync.  NodeID is UNINDEXED — carried through for JOIN lookups.

CREATE VIRTUAL TABLE IF NOT EXISTS node_fts USING fts5(
    NodeID    UNINDEXED,
    Title,
    PlainText
);


-- ── 2. Sync triggers ──────────────────────────────────────────────────────────
-- Fire on every PlainText/Title write to Node, keeping the FTS index current.
-- fts_backfill.py populates PlainText and inserts into node_fts on first run;
-- after that these triggers take over automatically.

-- Before a PlainText/Title update: remove the old FTS entry.
CREATE TRIGGER IF NOT EXISTS node_fts_before_update
BEFORE UPDATE OF PlainText, Title ON Node
WHEN old.PlainText IS NOT NULL
BEGIN
    DELETE FROM node_fts WHERE NodeID = old.NodeID;
END;

-- After a PlainText/Title update: insert the refreshed FTS entry.
CREATE TRIGGER IF NOT EXISTS node_fts_after_update
AFTER UPDATE OF PlainText, Title ON Node
WHEN new.PlainText IS NOT NULL
BEGIN
    INSERT INTO node_fts(NodeID, Title, PlainText)
    VALUES (new.NodeID, new.Title, new.PlainText);
END;

-- After a new Node is inserted with PlainText already populated.
CREATE TRIGGER IF NOT EXISTS node_fts_after_insert
AFTER INSERT ON Node
WHEN new.PlainText IS NOT NULL
BEGIN
    INSERT INTO node_fts(NodeID, Title, PlainText)
    VALUES (new.NodeID, new.Title, new.PlainText);
END;

-- Before a Node row is deleted: remove its FTS entry.
CREATE TRIGGER IF NOT EXISTS node_fts_before_delete
BEFORE DELETE ON Node
WHEN old.PlainText IS NOT NULL
BEGIN
    DELETE FROM node_fts WHERE NodeID = old.NodeID;
END;


-- ── Done ──────────────────────────────────────────────────────────────────────
-- Next step: run fts_backfill.py to extract text from DOCX blobs, populate
-- Node.PlainText, and insert rows into node_fts.
