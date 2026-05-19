# Database

## Purpose
One paragraph: what SQLite stores, why SQLite was chosen, and which
component is authoritative for which data.

## Scope
What this doc covers (schema, invariants, seed strategy, rebuild) and
what it does not (how services query — see [services.md](./services.md);
how the Plumber API exposes data — see [r-plumber.md](./r-plumber.md)).

## Key concepts
Define anything needed to read the schema: the linked-list model used
by `NodeHierarchy`, the BLOB storage convention for DOCX content, how
`HeaderNodeID` anchors a DocType's chain, etc.

## Schema
One subsection per table. For each, give the `CREATE TABLE` DDL (or a
fenced block summarizing columns + types), primary/foreign keys, and
any non-obvious invariants.

### DocType
```sql
CREATE TABLE DocType (
    DocTypeID      TEXT PRIMARY KEY,
    Name           TEXT NOT NULL,
    Type           TEXT NOT NULL DEFAULT 'main',  -- 'main' or 'attachment'
    InclusionTags  TEXT,                          -- JSON, see below; only on Type='main'
    HeaderNodeID   TEXT NULL,
    TemplateBlobID TEXT NULL,
    Tier           TEXT NULL,                     -- 'micro' | 'standard' | 'complex'; only on Type='main'
    FOREIGN KEY (HeaderNodeID)   REFERENCES Node(NodeID),
    FOREIGN KEY (TemplateBlobID) REFERENCES Node(NodeID)
);
```

Invariants:

-   `HeaderNodeID` must exist in `Node` and is the entry point into
    `NodeHierarchy` for this DocType's chain.
-   `Type` is `'main'` for user-facing document types and `'attachment'`
    for tag-triggered attachments (DEI, HAZMAT, etc.). The assembly
    picker hides `'attachment'` rows; they remain visible to admins.
    See ADR-005 in [decisions.md](./decisions.md).
-   `InclusionTags` on a `'main'` row is a JSON object mapping tag name
    to the `DocTypeID` of the `'attachment'` DocType that resolves it:
    `{"DEI": "<AttachmentDocTypeID>", "HAZMAT": "<AttachmentDocTypeID>"}`.
    Order of entries is the output order of the resulting attachments
    in the assembled DOCX.
-   `InclusionTags` is null or empty on `'attachment'` rows — attachment
    DocTypes do not themselves have inclusion tags (one level deep).
-   `Tier` is required by convention on `'main'` rows and one of
    `'micro'`, `'standard'`, `'complex'`. It is the build-from
    compatibility scope: any pair of `'main'` DocTypes sharing a
    `Tier` are eligible source/target pairs. `Tier` is NULL on
    `'attachment'` rows since attachments are never offered as
    build-from targets. The `tier` field already present in
    `Instance.InclusionData` JSON is a per-instance denormalized
    stamp echoing the source DocType's `Tier` at assembly time. See
    ADR-007 in [decisions.md](./decisions.md) and
    [features/build-from.md](./features/build-from.md).

### Node
DOCX blobs live in `Content`. Note any size limits or encoding rules.

### NodeHierarchy
Linked-list: `(DocTypeID, ParentNodeID) → ChildNodeID` with `Sequence`.
Invariant: each `(DocTypeID, ParentNodeID)` pair appears at most once.

### Instance
Assembled document records. `NodeList` is a JSON array of NodeIDs in
order of assembly.

## Seed data
What lives in `db/seed/` and how it gets into the DB. Mention the CSV
manifest files and which loader script reads each one.

## Dependencies
SQLite version, driver expectations (RSQLite for R, Microsoft.Data.Sqlite
for C#).

## Operational notes
Rebuild: `db/scripts/REBUILD_MAKEDOC_DATABASE.ps1`.
Schema changes: edit `db/sql/create_MAKEDOC_tables.sql` and re-run the
rebuild. Note any destructive behavior.

## Open questions
Schema evolution strategy, backup / migration policy, anything deferred.
