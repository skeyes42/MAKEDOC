.mode csv
PRAGMA foreign_keys = OFF;
PRAGMA ignore_check_constraints = ON;

.print *** LOADING CSV FROM: ../seed/seed_Node_table.csv ***
DELETE FROM Node;

-- Staging table matching the CSV columns (no PlainText)
CREATE TEMP TABLE node_import (
    NodeID          TEXT,
    NodeType        TEXT,
    Title           TEXT,
    Sequence        INTEGER,
    IsUserClause    INTEGER,
    IsSpecialClause INTEGER,
    DerivedFrom     TEXT,
    Content         BLOB
);

.import --skip 1 ../seed/seed_Node_table.csv node_import

INSERT INTO Node (NodeID, NodeType, Title, Sequence, IsUserClause, IsSpecialClause, DerivedFrom, Content)
SELECT             NodeID, NodeType, Title, Sequence, IsUserClause, IsSpecialClause, DerivedFrom, Content
FROM node_import;

DROP TABLE node_import;

PRAGMA foreign_keys = ON;