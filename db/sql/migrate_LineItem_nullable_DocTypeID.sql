-- Migration: make LineItem.DocTypeID nullable (instance-keyed line items).
-- SQLite cannot alter column constraints, so rebuild the table and copy data.
-- Run against the existing MAKEDOC.db, e.g.:
--   sqlite3 MAKEDOC.db < migrate_LineItem_nullable_DocTypeID.sql

PRAGMA foreign_keys = OFF;
BEGIN TRANSACTION;

CREATE TABLE LineItem_new (
    LineItemID   TEXT PRIMARY KEY,
    DocTypeID    TEXT NULL,
    InstanceID   TEXT NULL,
    LineNum      INTEGER NOT NULL,
    Description  TEXT NOT NULL,
    NAICS        INTEGER DEFAULT 0,
    Unit         TEXT NOT NULL,
    Quantity     REAL NOT NULL,
    UnitPrice    REAL NOT NULL,

    FOREIGN KEY (DocTypeID)  REFERENCES DocType(DocTypeID),
    FOREIGN KEY (InstanceID) REFERENCES Instance(InstanceID),
    CHECK (
        (DocTypeID IS NOT NULL AND InstanceID IS NULL) OR
        (DocTypeID IS NULL AND InstanceID IS NOT NULL)
    )
);

-- Existing rows may have both DocTypeID and InstanceID set (old schema).
-- Prefer instance ownership; otherwise keep doctype staging.
INSERT INTO LineItem_new
    (LineItemID, DocTypeID, InstanceID, LineNum, Description, NAICS, Unit, Quantity, UnitPrice)
SELECT
    LineItemID,
    CASE WHEN InstanceID IS NOT NULL THEN NULL ELSE DocTypeID END,
    InstanceID,
    LineNum, Description, NAICS, Unit, Quantity, UnitPrice
FROM LineItem;

DROP TABLE LineItem;
ALTER TABLE LineItem_new RENAME TO LineItem;

COMMIT;
PRAGMA foreign_keys = ON;
