# Fill-in assembly flow

## Purpose

One paragraph: how MAKEDOC discovers fill-in variables in the clauses
that make up a document, prompts the user for values, re-assembles the
document with those values substituted, and persists the mapping on
the `Instance` row. Authoring of fill-in SDTs happens in the clause
editor (see [clause-node-editing.md](./clause-node-editing.md)); this
document covers what happens at assembly time.

## Scope

Covered here: the runtime flow during document assembly — scanning
the ordered clause list for plain-text SDTs, collecting the distinct
variable names, prompting the user, substituting values, writing the
`Instance` row (including `FillinData`), and emitting the DOCX. Not
covered: authoring of SDTs onto clauses (see
[clause-node-editing.md](./clause-node-editing.md)); the schema for
`Instance` and related tables (see [database.md](../database.md));
the legacy R `/assemble` endpoint, which is superseded by client-side
assembly (ADR-002).

**Pedagogical scope note.** Validation of fill-in values (type
checks, required-field enforcement, format constraints) is
*discussed* but intentionally *not implemented*, consistent with the
MAKEDOC book's focus.

## Use case

**Scenario: assemble a Standard Solicitation with fill-ins.**

1. User opens `AssemblyForm`, picks the Standard Solicitation
   DocType, and starts assembly.
2. MAKEDOC walks the `NodeHierarchy` chain for that DocType, pulls
   the ordered list of clause `Node`s, and scans each clause's
   DOCX for plain-text SDTs. It collects the distinct variable names
   — say `{contractor_name, delivery_days, effective_date}`.
3. MAKEDOC presents a fill-in form listing those three variables.
   Each is an empty text box labeled with the variable name.
4. If this assembly has a `BuildFromID` or `PrevEditionID`, the form
   is pre-populated from that instance's `FillinData`.
5. User supplies values and confirms.
6. MAKEDOC assembles the final DOCX, substituting the SDT text for
   each occurrence with the user's value. A single variable used in
   multiple clauses gets the same substituted value everywhere.
7. A new `Instance` row is written: `FillinData` holds the JSON map
   of what the user supplied, `NodeList` holds the ordered node IDs.
8. The DOCX is delivered to the user.

**Acceptance:**

-   A variable that appears in two clauses is prompted for once and
    substituted everywhere.
-   `Instance.FillinData` JSON keys match the variable names scanned
    from the clauses.
-   Re-assembling the same DocType without fill-ins (no SDTs in any
    clause) skips the prompt entirely and produces a DOCX directly.

## Key concepts

-   **Fill-in variable** — a name that appears as the text of a
    plain-text SDT inside one or more clauses in the assembly list.
-   **Variable set** — the distinct union of variable names across
    all clauses in the ordered `NodeHierarchy` for the DocType being
    assembled. This drives the prompt form.
-   **Substitution** — replacing the SDT's text content with the
    user-supplied value. The SDT element itself is preserved (so the
    resulting document remains Word-compatible and the fill can be
    re-opened in the future by diffing SDT presence, if we ever want
    that).

## Data flow

```
DocType selected
  → walk NodeHierarchy → ordered [NodeID...]
  → for each Node: read Content blob, find plain-text SDTs,
                    collect SDT text values
  → distinct variable names = variable set
  → if variable set is empty:
        assemble DOCX, write Instance, done
    else:
        build/update fill-in form with one input per variable
        (pre-fill from BuildFromID / PrevEditionID FillinData if any)
        → user submits values
        → re-walk Nodes, substitute SDT text with values
        → assemble DOCX
        → write Instance row:
              FillinData = JSON({name: value, ...})
              NodeList   = JSON([NodeID, ...])
        → deliver DOCX
```

## Data model impact

No schema change. The flow uses existing `Instance` columns:

-   `FillinData` — JSON object, keys are variable names (as scanned
    from SDT text), values are user-supplied strings. Example:
    `{"delivery_days": "30", "contractor_name": "ABC Corp"}`.
-   `NodeList` — JSON array of NodeIDs in assembly order, already
    defined.
-   `BuildFromID` / `PrevEditionID` — used to locate a prior
    `Instance` whose `FillinData` should pre-fill the form.

See [database.md](../database.md) for the full `Instance` DDL.

## Service surface

New or extended operations in `MakeDoc.Core.Services`:

-   `IReadOnlyList<string> CollectFillinNames(IEnumerable<Guid>
    nodeIds)` — scan the ordered clause list and return the distinct
    variable names. Order-preserving by first appearance (so the
    form can present variables in reading order of the assembled
    document).
-   `byte[] AssembleWithFillins(Guid docTypeId, IReadOnlyDictionary<
    string, string> values)` — walk the node list, substitute SDT
    text per `values`, return the DOCX bytes. Called after the user
    submits the form.
-   `Guid RecordInstance(Guid docTypeId, IReadOnlyList<Guid>
    nodeIds, IReadOnlyDictionary<string, string> fillinValues, Guid?
    buildFromId, Guid? prevEditionId)` — writes the `Instance` row
    with `FillinData` serialized to JSON.

A dedicated `FillinService` or a section of an expanded
`AssemblyService` is a reasonable home; pick during implementation.

## UI

The fill-in prompt lives inside the assembly flow. Two reasonable
shapes:

-   **Inline panel inside `AssemblyForm`** — the form first shows
    the DocType picker, then (if the variable set is non-empty)
    reveals a fill-in panel, then enables "Assemble."
-   **Modal/child form opened from `AssemblyForm`** — a separate
    `FillinForm` launched once the variable set is known.

Either way, the form is built dynamically from the scanned variable
set. Unknown in advance how many inputs there'll be, so the layout
needs to handle zero (no fill-ins → skip) up to many.

Add the chosen shape to [forms.md](../forms.md) once settled.

## Alternatives considered

-   **Keep `/assemble` on the R Plumber side.** Rejected by
    ADR-002. Client-side assembly lets us use .NET DOCX tooling
    (Clippit, OpenXml) where it's mature, and avoids shipping DOCX
    blobs across process boundaries. R remains the home of document
    analytics, where its ecosystem is stronger.
-   **Use typed SDTs (date picker, dropdown, etc.).** Rejected for
    the book's scope — see
    [clause-node-editing.md](./clause-node-editing.md). The fill-in
    form would be richer but the scanner and substitution logic
    would carry more cases.
-   **Store fill-in definitions on the clause row (new columns) and
    look them up without scanning.** Rejected — the DOCX blob is
    the source of truth; a second copy invites drift.
-   **Prompt per-clause instead of for the union.** Rejected — the
    same variable across clauses should be supplied once, not
    repeatedly.

## Open questions

-   **Missing values.** If the user leaves a variable blank, do we
    refuse to assemble, assemble with an empty string, or leave the
    SDT text as the variable name (visible placeholder)?
-   **Unknown variables on re-fill.** When pre-filling from a prior
    `Instance`, if that instance's `FillinData` contains keys that
    are no longer in the clause set (a clause was edited), do we
    drop them silently, warn, or preserve them for audit?
-   **New variables on re-fill.** Conversely, if new variables
    appeared in the clauses since the prior instance, do we prompt
    only for the new ones, or re-show all?
-   **Variable name rules.** Do we constrain characters / length?
    Enforced where — authoring (clause editor) or assembly
    (scanner)? (This ties the validation discussion to the authoring
    side.)
-   **Re-editing `Instance.FillinData` post hoc.** Does the archive
    flow let a user open an `Instance`, change fill-in values, and
    regenerate? Or are `Instance` rows immutable once written?
-   **Validation (discussed, not implemented):** at authoring or at
    assembly? What's the error surface if a date SDT ever got
    misused (blocked in authoring, caught at assembly)?

## Related

-   [decisions.md](../decisions.md) — ADR-002 (client-side
    assembly), ADR-004 (fill-in SDT form + flow).
-   [clause-node-editing.md](./clause-node-editing.md) — how
    fill-in SDTs get onto clauses in the first place.
-   [architecture.md](../architecture.md) — fill-ins as a top-level
    MAKEDOC capability.
-   [services.md](../services.md) — where the scanner / assembler
    / recorder live.
-   [forms.md](../forms.md) — where the fill-in prompt UI lands.
-   [database.md](../database.md) — `Instance.FillinData`,
    `NodeList`, `BuildFromID`, `PrevEditionID`.
