-- =============================================
-- DOCMAKE Database Schema 3.9.2026 - SQLite3
-- =============================================

-- Node Table (created first to avoid circular dependency)
CREATE TABLE Node (
    NodeID TEXT PRIMARY KEY,
    NodeType TEXT NOT NULL CHECK(NodeType IN ('Document', 'Section', 'Subsection', 'Clause', 'HeaderNode', 'Template')),
    Title TEXT NULL,
    Sequence INTEGER NOT NULL DEFAULT 0,
    IsUserClause INTEGER NOT NULL DEFAULT 0,
    IsSpecialClause INTEGER NOT NULL DEFAULT 0,
    DerivedFrom TEXT NULL,
    Content BLOB NULL,

    FOREIGN KEY (DerivedFrom) REFERENCES Node(NodeID)
);

-- NodeHierarchy Table
CREATE TABLE NodeHierarchy (
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
CREATE TABLE DocType (
    DocTypeID TEXT PRIMARY KEY,
    Type TEXT NOT NULL,
    Name TEXT NOT NULL,
    InclusionTags TEXT, -- JSON array: ["DEI", "HAZMAT", "INTERNATIONAL"]
    HeaderNodeID TEXT NULL,
	TemplateBlobID TEXT NULL,
    Tier TEXT NULL, -- 'micro' | 'standard' | 'complex'; required by convention on Type='main', NULL on Type='attachment'. See ADR-007.

    FOREIGN KEY (HeaderNodeID) REFERENCES Node(NodeID),
	FOREIGN KEY (TemplateBlobID) REFERENCES Node(NodeID)
);

-- Instance Table (DocumentInstance)
CREATE TABLE Instance (
    InstanceID TEXT PRIMARY KEY,
    DocTypeID TEXT NOT NULL,
    PrevEditionID TEXT NULL,
    BuildFromID TEXT NULL,
    GeneratedDate TEXT DEFAULT (datetime('now')),
    IsArchived INTEGER DEFAULT 0, -- 0 = active, 1 = archived
    ArchiveDate TEXT NULL,
    Title TEXT NOT NULL DEFAULT '',
    OutputFile TEXT NULL,
        
    -- JSON data columns
    InclusionData TEXT, -- JSON: {"tags": ["DEI", "HAZMAT"], "tier": "standard"}
    FillinData TEXT, -- JSON: {"delivery_days": "30", "contractor_name": "ABC Corp"}
    NodeList TEXT, -- JSON array of NodeIDs included: [NL-0001, NL-0003, NL-0005, NL-0012, NL-0047, NL-0082]

    FOREIGN KEY (DocTypeID)     REFERENCES DocType(DocTypeID), 
    FOREIGN KEY (PrevEditionID) REFERENCES Instance(InstanceID),
    FOREIGN KEY (BuildFromID)   REFERENCES Instance(InstanceID)
);
