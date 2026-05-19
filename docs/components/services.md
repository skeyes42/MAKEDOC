# Services

## Purpose
One paragraph: the role of `MakeDoc.Core.Services` — the business-logic
and data-access layer sitting between the WinForms UI and the SQLite
database / Plumber API.

## Scope
Covered here: each service's responsibility, public surface, and
dependencies. Not covered: UI wiring (see [forms.md](./forms.md)),
schema details (see [database.md](./database.md)), HTTP contract for the
Plumber layer (see [r-plumber.md](./r-plumber.md)).

## Key concepts
Conventions used across services: connection lifetime, transactions,
error-propagation (exceptions vs. result types), async/sync stance.

## Services
One subsection per service. For each: purpose, public methods with
signatures, key exceptions, what it depends on.

### DatabaseSetupServices
Creates / drops / seeds the SQLite DB. Used by admin tooling and the
rebuild script.

### NodeService
CRUD over the `Node` table. DOCX blob read/write.

### NodeHierarchyService
Manages `NodeHierarchy` edges. Hosts `GetOrderedNodeIds()` — the
linked-list walk that the Plumber `/assemble` endpoint reimplements in R.

### NodeLoaderService
Bulk import of Nodes from source files (CSV manifests, DOCX templates).
Returns a `NodeLoaderResult` summary.

### SeedDataService
Loads the CSV seed files in `db/seed/` into their target tables.

### TemplateService
Template-specific operations (listing, previewing, swapping the
DOCX blob on a Template node).

## Models
Lightweight DTOs in `MakeDoc.Core.Models`: `DocType`, `Node`,
`NodeHierarchy`, `Instance`, `NodeLoaderResult`. Describe each one's
fields if it's not obvious from the name.

## Dependencies
`Microsoft.Data.Sqlite` for DB access, `DocumentFormat.OpenXml` for
DOCX manipulation on the C# side, the Plumber client for any operation
delegated to R.

## Operational notes
Connection string / DB path resolution (env var? config file?),
transaction boundaries, logging.

## Open questions
How to surface errors from the Plumber layer (now analytics-only) to
callers cleanly.

(Resolved: where assembly runs. ADR-002 put assembly on the C# client;
ADR-004 confirms R/Plumber is narrowed to analytics. See
[decisions.md](./decisions.md) and
[features/fill-in-assembly.md](./features/fill-in-assembly.md).)
