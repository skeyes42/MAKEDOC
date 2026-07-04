# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MAKEDOC is a .NET 9 WinForms desktop application for assembling procurement documents from a clause library stored in SQLite. Documents are built by stitching DOCX blobs together and substituting fill-in variables. An optional R Plumber HTTP API provides analytics.

## Build & Run

```powershell
# Build
dotnet build

# Run the desktop app
dotnet run --project MakeDoc.App

# Run headless batch generator
dotnet run --project MakeDoc.Batch -- --count 1000 --out C:\temp\assembled_documents --seed 42

# Run tests
dotnet test MakeDoc.Core.Tests
```

**Required environment variable:** `MAKEDOC_DB` must point to the directory containing `MAKEDOC.db` (e.g. `C:\Users\skeye\BOOK2\MAKEDOC\db`). The app checks this at startup via `DatabaseSetupService`.

**Start the R analytics API** (optional, needed for Analytics form):
```r
library(plumber)
pr <- plumb("C:/Users/skeye/BOOK2/MAKEDOC/plumber/api.R")
pr$run(host = "127.0.0.1", port = 8000)
```

**Rebuild the database from seed data:**
```powershell
cd db\scripts
.\REBUILD_MAKEDOC_DATABASE.ps1
# Requires: MAKEDOC_DB env var, sqlite3.exe, R/Rscript, Excel COM, Python
```

## Architecture

### Layer Stack

```
MakeDoc.App          — WinForms UI (net9.0-windows, nullable: false)
MakeDoc.Core         — Business logic, services, data access (nullable: annotations)
MakeDoc.PlumberClient — HTTP wrapper for R Plumber API
MakeDoc.Batch        — Headless CLI for bulk document generation
MakeDoc.Core.Tests   — xUnit tests
```

`MakeDoc.Core` owns all database access through `MakDocDb.cs` (a thin repository over `Microsoft.Data.Sqlite`). The UI calls core services directly in-process; there is no intermediary API layer for the desktop app.

### Key Domain Models (`MakeDoc.Core/Models/`)

| Model | Purpose |
|---|---|
| `Node` | A clause/section; DOCX content stored as `Content (BLOB)` |
| `DocType` | Document template definition; has `Tier` (micro/standard/complex) and `InclusionTags` (JSON) |
| `NodeHierarchy` | Linked-list edges: `ParentNodeID → ChildNodeID` with `Sequence` per `DocTypeID` |
| `Instance` | Assembled document record; stores `NodeList`, `FillinData`, `InclusionData` as JSON columns |
| `LineItem` | Procurement table rows scoped to a DocType or Instance |

### Document Assembly Flow

1. User selects a `DocType` → UI loads canonical `NodeHierarchy`
2. User edits node list (add/remove/reorder clauses)
3. "Generate" → `DocumentAssemblyService` fetches DOCX blobs from SQLite
4. Blobs merged via in-memory ZIP manipulation: styles/numbering/theme copied verbatim; only `word/document.xml` bodies are appended
5. `FillinService` scans merged XML for Word content controls (SDT elements, `w:alias` attribute) and substitutes values
6. Output DOCX written to disk; `Instance` record persisted with JSON columns

### Fill-In Variables

Variables are marked in Word via **content control Title** field (`w:sdtPr/w:alias`). The service also handles plain-text patterns like `{SecNo}` and `{lineitem}` (the latter renders a full table from `LineItem` records).

### Build-From Feature

An `Instance` can be cloned from another (`BuildFromID FK → Instance`). Only DocTypes of the same `Tier` can be used as build-from sources. The denormalized tier value is stored in `Instance.InclusionData` at assembly time.

## Database Conventions

- `PRAGMA foreign_keys = ON` is enforced at init
- `Instance` rows are **never deleted** — use `IsArchived = 1` instead
- Node ID prefixes: `NL-` (library clauses), `DT-` (DocTypes), `UC-` (user clauses, GUID suffix)
- JSON columns (`InclusionData`, `FillinData`, `NodeList`) are raw strings, parsed manually with `System.Text.Json`
- FTS5 full-text search is available via `fts_backfill.py` and `db/sql/fts_migrate.sql`

## DOCX Assembly Constraints

- No BOM in XML output — Word's OPC parser rejects BOMs
- Assembly order is strictly determined by `NodeHierarchy.Sequence`
- The merge strategy is ZIP-level: only `word/document.xml` body elements are appended; all other parts (styles, numbering, theme) come from the first (header) node's template

## Project-Specific Notes

- The solution file is `MAKEDOC.slnx` (the newer XML-based format), not `.sln`
- `docs/` contains comprehensive architecture docs including ADRs — check `docs/decisions/decisions.md` before making structural changes
- Analytics (`cluster_docs.R`, `clause_search.py`) operate on the same SQLite DB directly
- `MakeDoc.App` sets `nullable: false`; `MakeDoc.Core` sets `nullable: annotations` — keep this distinction when editing each project

# Project layout for generating prompts 
C:\Users\skeye\BOOK2\MAKEDOC\docs\prompting
    /json_data
        complex-generate-award-from-solicitation-parms.json
        complex-generate-canonical-requisition-parms.json
        complex-generate-solicitation-from-requisition-parms.json
        micro-generate-award-from-solicitation-parms.json
        micro-generate-canonical-requisition-parms.json
        micro-generate-solicitation-from-requisition-parms.json
        standard-generate-award-from-solicitation-parms.json
        standard-generate-canonical-requisition-parms.json
        standard-generate-solicitation-from-requisition-parms.json

    /parameterized_prompts
        generate-award-from_solicitation.md
        generate-canonical-requisition.md
        generate-solicitation-from_requisition.md

    /generation_script/
        generation_script.py

