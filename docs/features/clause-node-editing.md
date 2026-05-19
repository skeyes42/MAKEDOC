# Clause node editing

## Purpose

One paragraph: authors need to edit existing clause nodes for two
distinct reasons — to correct or improve the clause content (typos,
clarity, rewording) and to author fill-ins by placing plain-text SDT
fields into the clause. Both reasons are served by a single dedicated
editor form in MAKEDOC. This closes the loop on the fill-in capability
described in [architecture.md](../architecture.md) and removes the
need to round-trip clauses through Word for routine edits. The
companion runtime flow — how fill-ins are discovered and prompted at
assembly time — lives in [fill-in-assembly.md](./fill-in-assembly.md).

## Scope

Covered here: a dedicated clause-editing form that supports both
content edits to `Node.Content` and inserting / editing / removing
plain-text SDTs that represent fill-in variables. Not covered: the
fill-in runtime (see [fill-in-assembly.md](./fill-in-assembly.md));
bulk import of clauses via `NodeLoaderService`; editing of non-clause
nodes such as Templates.

**Pedagogical scope note.** Fill-in validation (enforcing types,
required fields, format constraints at authoring or assembly time) is
*discussed* in this doc but intentionally *not implemented*, in
keeping with the MAKEDOC book's focus on the system's shape rather
than production-grade form handling. The design should leave room for
validation to be added later without reshaping the data model.

## Use case

Author's-eye view. Two scenarios — one for each reason to edit.

**Scenario A: fix a typo in the Termination clause.**

1. Author opens the clause editor from the main dashboard and picks
   the Termination clause.
2. Author corrects the typo in the content area and saves.
3. The updated DOCX blob is written back to `Node.Content`.

**Scenario B: add a date fill-in to the Termination clause.**

1. Author opens the same clause in the editor.
2. Author places the cursor where the fill-in should go, chooses
   "Insert fill-in," and types the variable name —
   `effective_date`. A plain-text SDT is inserted at that position
   whose text is `effective_date`.
3. Author saves.
4. On the next assembly of a DocType that includes this clause, the
   fill-in flow (see [fill-in-assembly.md](./fill-in-assembly.md))
   picks up `effective_date`, prompts the user for a value, and
   substitutes it into the assembled document.

**Acceptance:**

-   Content edits round-trip through the editor without disturbing
    existing fill-in SDTs.
-   Round-tripping an edited clause through Word preserves the
    plain-text SDTs (their variable-name text is intact).
-   The same variable name used in multiple places inside one clause
    (or across clauses) survives the round trip with each occurrence
    still a plain-text SDT.

## Key concepts

-   **Clause node** — a `Node` row whose `Content` holds the clause's
    DOCX blob. Clauses are distinguished from Templates and
    HeaderNodes (see [database.md](../database.md)).
-   **Fill-in SDT** — a Word plain-text Structured Document Tag whose
    text content is the **name of the fill-in variable** (e.g.
    `effective_date`, `contractor_name`). MAKEDOC uses only
    plain-text SDTs; no rich text, date pickers, dropdowns, etc.
-   **Variable name as identity** — the SDT's text *is* the fill-in
    key. MAKEDOC does not rely on the SDT's `w:tag` or `w:alias`
    attributes to identify fill-ins. Two SDTs with the same text
    represent the same variable, whether in one clause or several.

## Data model impact

No schema change. Both content edits and SDT edits write through to
the existing `Node.Content` DOCX blob, which remains the source of
truth. The fill-in values collected at assembly time are persisted in
`Instance.FillinData`, not on the clause — see
[fill-in-assembly.md](./fill-in-assembly.md) and
[database.md](../database.md).

## Service surface

New or extended operations in `MakeDoc.Core.Services`. `NodeService`
already does CRUD and DOCX blob read/write (see
[services.md](../services.md)); the new work is a set of SDT-aware
helpers on top. Because the SDT shape is fixed (plain-text,
text = variable name), the surface stays simple.

Sketch (final shape to be settled during implementation):

-   `byte[] GetContent(Guid nodeId)` / `void SetContent(Guid nodeId,
    byte[] docx)` — existing blob read/write, used by the editor for
    content edits.
-   `IReadOnlyList<string> ListFillinNames(Guid nodeId)` — parse the
    clause's DOCX and return the set of variable names found in its
    plain-text SDTs.
-   `void InsertFillin(Guid nodeId, string variableName,
    SdtLocation at)` — insert a plain-text SDT at a location, with
    the given variable name as its text.
-   `void RenameFillin(Guid nodeId, string oldName, string newName)` —
    rename every occurrence of a variable within the clause.
-   `void RemoveFillin(Guid nodeId, string variableName)` — remove
    every occurrence within the clause.

Implementation leans on the same DOCX/OpenXml stack used by the C#
assembly path (ADR-002).

## UI

A **dedicated form** — tentatively `ClauseEditorForm` — reached from
the main dashboard (and optionally from `AdminForm` when the user
drills into a clause row). Content editing and fill-in management are
sibling capabilities inside the same form, not separate screens.

Likely shape: a content area showing the clause, with a "fill-ins"
side panel listing the variable names currently used in this clause
and supporting insert / rename / remove. The "insert" action prompts
for a variable name and drops a plain-text SDT at the caret. Save
writes the full DOCX blob back through `NodeService`.

Add a `ClauseEditorForm` subsection to [forms.md](../forms.md) once
the form exists.

## Alternatives considered

-   **Extend `AdminForm` with inline edit.** Rejected. The editor's
    surface — content editing plus fill-in management — would crowd
    `AdminForm`'s CRUD-grid responsibility.
-   **Richer SDT palette (date, dropdown, checkbox).** Rejected for
    the book's scope. Plain-text SDTs keep the authoring UI, the
    service surface, and the runtime scanner simple. Typed fill-ins
    are a natural extension if MAKEDOC later grows past pedagogical
    scope.
-   **Use `w:tag` (not the SDT text) as the fill-in key.** Rejected.
    Tag is invisible in Word, which would make authoring harder and
    would hide the variable identity from reviewers reading the
    DOCX.
-   **Author everything in Word, re-import via `NodeLoaderService`.**
    Works today but forces a context switch and a full round-trip
    for routine edits, including typo fixes.
-   **Store fill-in definitions outside the DOCX (new table), merge
    at assembly time.** Diverges from Word's native fill-in model
    and complicates round-tripping clauses through Word.

## Open questions

-   What content-editing control hosts the clause body — a WinForms
    `RichTextBox` with a DOCX round-trip, a hosted Word control, or
    something else?
-   Does the editor's scope ever widen to non-clause nodes
    (Templates)? If so, should the form be renamed `NodeEditorForm`
    up front?
-   Undo / history: does editing a clause create a new `Node` row,
    or overwrite in place? (Interaction with archive / audit.)
-   **Validation (discussed, not implemented):** if we were to add
    it later, would we validate at authoring time (e.g. warn on
    suspicious variable names, empty names, collisions) or defer
    entirely to the assembly-time form in
    [fill-in-assembly.md](./fill-in-assembly.md)? Left open on
    purpose.

## Related

-   [decisions.md](../decisions.md) — ADR-003 (in-app clause
    editing), ADR-004 (fill-in SDT form + assembly flow).
-   [fill-in-assembly.md](./fill-in-assembly.md) — the companion
    runtime flow.
-   [architecture.md](../architecture.md) — fill-ins as a top-level
    MAKEDOC capability.
-   [services.md](../services.md) — where the new service methods
    land.
-   [forms.md](../forms.md) — where the editing UI lands.
-   [database.md](../database.md) — `Node` table, `Content` blob
    convention.
