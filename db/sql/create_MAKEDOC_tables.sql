-- =============================================
-- DOCMAKE Database Schema 3.9.2026 - SQLite3
-- =============================================

-- Node Table (created first to avoid circular dependency)
CREATE TABLE IF NOT EXISTS Node (
    NodeID TEXT PRIMARY KEY,
    NodeType TEXT NOT NULL CHECK(NodeType IN ('Document', 'Clause', 'HeaderNode')),
    Title TEXT NULL,
    Sequence INTEGER NOT NULL DEFAULT 0,
    IsUserClause INTEGER NOT NULL DEFAULT 0,
    IsSpecialClause INTEGER NOT NULL DEFAULT 0,
    DerivedFrom TEXT NULL,
    Content BLOB NULL, PlainText TEXT NULL,

    FOREIGN KEY (DerivedFrom) REFERENCES Node(NodeID)
);
CREATE INDEX idx_Node_NodeType ON Node(NodeType);
CREATE INDEX idx_Node_Sequence ON Node(Sequence);
CREATE TRIGGER node_fts_before_update
BEFORE UPDATE OF PlainText, Title ON Node
WHEN old.PlainText IS NOT NULL
BEGIN
    DELETE FROM node_fts WHERE NodeID = old.NodeID;
END;
CREATE TRIGGER node_fts_after_update
AFTER UPDATE OF PlainText, Title ON Node
WHEN new.PlainText IS NOT NULL
BEGIN
    INSERT INTO node_fts(NodeID, Title, PlainText)
    VALUES (new.NodeID, new.Title, new.PlainText);
END;
CREATE TRIGGER node_fts_after_insert
AFTER INSERT ON Node
WHEN new.PlainText IS NOT NULL
BEGIN
    INSERT INTO node_fts(NodeID, Title, PlainText)
    VALUES (new.NodeID, new.Title, new.PlainText);
END;
CREATE TRIGGER node_fts_before_delete
BEFORE DELETE ON Node
WHEN old.PlainText IS NOT NULL
BEGIN
    DELETE FROM node_fts WHERE NodeID = old.NodeID;
END;

-- NodeHierarchy Table
CREATE TABLE IF NOT EXISTS NodeHierarchy (
    ParentNodeID  TEXT NOT NULL,
    ChildNodeID   TEXT NOT NULL,
    DocTypeID     TEXT NOT NULL,   -- which document type this relationship belongs to
    Sequence      INTEGER NOT NULL DEFAULT 0,

    PRIMARY KEY (ParentNodeID, ChildNodeID, DocTypeID),
    FOREIGN KEY (ParentNodeID) REFERENCES Node(NodeID),
    FOREIGN KEY (ChildNodeID)  REFERENCES Node(NodeID),
    FOREIGN KEY (DocTypeID)    REFERENCES DocType(DocTypeID)
);

-- DocType Table
CREATE TABLE IF NOT EXISTS DocType (
    DocTypeID TEXT PRIMARY KEY,
    Type TEXT NOT NULL,
    Name TEXT NOT NULL,
    InclusionTags TEXT, -- JSON array: ["DEI", "HAZMAT", "INTERNATIONAL"]
    HeaderNodeID TEXT NULL,
    Tier TEXT NULL, -- 'micro' | 'standard' | 'complex'; required by convention on Type='main', NULL on Type='attachment'. See ADR-007.

    FOREIGN KEY (HeaderNodeID) REFERENCES Node(NodeID)
);

-- Instance Table (DocumentInstance)
CREATE TABLE IF NOT EXISTS Instance (
    InstanceID TEXT PRIMARY KEY,
    DocTypeID TEXT NOT NULL,
    PrevEditionID TEXT NULL,
    BuildFromID TEXT NULL,
    GeneratedDate TEXT DEFAULT (datetime('now')),
    IsArchived INTEGER DEFAULT 0, -- 0 = active, 1 = archived
    ArchiveDate TEXT NULL,
    Title TEXT NOT NULL DEFAULT '',
        
    -- JSON data columns
    InclusionData TEXT, -- JSON: {"tags": ["DEI", "HAZMAT"], "tier": "standard"}
    FillinData TEXT, -- JSON: {"delivery_days": "30", "contractor_name": "ABC Corp"}
    NodeList TEXT, -- JSON array of NodeIDs included: [NL-0001, NL-0003, NL-0005, NL-0012, NL-0047, NL-0082]

    FOREIGN KEY (DocTypeID)     REFERENCES DocType(DocTypeID), 
    FOREIGN KEY (PrevEditionID) REFERENCES Instance(InstanceID),
    FOREIGN KEY (BuildFromID)   REFERENCES Instance(InstanceID)
);

CREATE TABLE IF NOT EXISTS LineItem (
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

