# R Plumber API

## Purpose

MAKEDOC exposes a local R Plumber service in order to demonstrate the client/server architecture of the system. The MAKEDOC database (which contains the ingredients of the documents – the clauses, sections, etc.) and the R service are all on the same machine.

## Scope

Covered here: HTTP contract, request/response shapes, error model, startup. Not covered: the SQL schema (see [database.md](./database.md)) or how the C\# client consumes this API (see [services.md](./services.md)).

## Key concepts

Vocabulary the endpoints use: Node, DocType, HeaderNodeID, Instance, linked-list traversal of NodeHierarchy, DOCX blob.

## Endpoints

### GET /health

Liveness + DB reachability check. Returns JSON:

```json
{ "status": "ok", "database": "connected — N document type(s)", "time": "..." }
```

503 on DB failure.

### GET /doctypes

Returns all DocType rows as a JSON array. Used by the WinForms AssemblyForm to populate its picker.

### GET /instances

Returns all non-archived Instances joined to DocType for display.

### POST /assemble

Assembles a DOCX for a given DocType.

**Request:**

```json
{ "docTypeId": "<DocTypeID>" }
```

**Response (success):** Streams the DOCX bytes with `Content-Type: application/vnd.openxmlformats-officedocument.wordprocessingml.document`. Also inserts an Instance row.

**Response (error):** HTTP 400 (bad input) or 500 (server error) with JSON body:

```json
{ "error": "..." }
```

## Data flow

Walk through `/assemble`: parse body → validate DocType → walk NodeHierarchy chain from HeaderNodeID → pull `Content` BLOBs in order → merge DOCXs in `assemble.R` → insert Instance → stream bytes.

## Files

-   `plumber/api.R` — route definitions.
-   `plumber/db.R` — SQLite helpers, NodeHierarchy traversal.
-   `plumber/assemble.R` — DOCX merge using xml2 + zip.

## Dependencies

R packages: `plumber`, `DBI`, `RSQLite`, `jsonlite`, `xml2`, `zip`, `uuid`. Install with `install.packages(c(...))`.

## Operational notes

Required env var: `MAKEDOC_DB` — the directory containing `MAKEDOC.db`.

Start the server:

```r
Sys.setenv(MAKEDOC_DB = "C:/Users/skeye/BOOK2/MAKEDOC/db")
library(plumber)
pr <- plumb("C:/Users/skeye/BOOK2/MAKEDOC/plumber/api.R")
pr$run(host = "127.0.0.1", port = 8000)
```

Default port: 8000. Bind to `127.0.0.1` for local-only; use `0.0.0.0` only if the client is on a different host.

## Error model

All error responses are JSON with an `error` field, including on routes whose success response is binary (`/assemble`). HTTP status codes: `400` for validation failures, `500` for unexpected server errors, `503` for DB unreachable on `/health`.

## Open questions

Authentication, lifecycle (who starts/stops the server — the WinForms app, a service, the user?), logging strategy, what to do on partial assembly failures.
