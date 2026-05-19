# Forms

## Purpose
One paragraph: the responsibility of the WinForms UI layer in MAKEDOC
and the line between "form logic" and "service logic".

## Scope
Covered here: each form's job, navigation between forms, how forms
consume services. Not covered: business rules (see
[services.md](./services.md)), data model (see [database.md](./database.md)).

## Key concepts
Form lifecycle conventions (who owns the DB connection, who owns the
HTTP client, how forms are disposed), and any shared base classes or
user-control patterns.

## Forms
One subsection per form. For each: its purpose, the services it calls,
the main user flows it supports.

### MainDashboard
Entry point. Centered on the **Instance table** — the list of prior
assembled documents, joined to `DocType` for display name and `Tier`.
Active rows are selectable; archived rows are visible but rendered at
reduced opacity / italic and not selectable (per ADR-007 and
[features/build-from.md](./features/build-from.md)). Secondary entry
points to the other forms — `AdminForm`, `AnalyticsForm`,
`ArchiveForm`, `AssemblyForm` — live in the top menu bar
(`File`, `Tools`).

Currently read-only. The **+ New Document** button (above the table),
the **Build from this** row action, and the build-from picker /
*Build what?* step are deferred to subsequent slices of the build-from
feature.

### AdminForm
CRUD over Nodes / DocTypes / NodeHierarchy.

### AnalyticsForm
Reports on instances, node usage, etc.

### ArchiveForm
Manages archived instances.

### AssemblyForm
Primary document-assembly flow. Calls `POST /assemble` on the Plumber
API via `MakeDoc.PlumberClient`.

## Navigation
How the forms connect. A small diagram or flow list is useful here.

## Dependencies
Services consumed from `MakeDoc.Core`, the Plumber client, any UI
libraries beyond stock WinForms.

## Operational notes
How to run the app in Debug, where settings live, how to point it at
a running Plumber server (host / port config).

## Open questions
Open UX questions, form-ownership ambiguities, refactors deferred.
