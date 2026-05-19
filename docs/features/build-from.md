# Build-from

## Purpose

One paragraph: users frequently start a new document from an
existing one — another school bus requisition shaped after the last
one (same DocType, with the user's structural edits preserved), or
a follow-on solicitation that mirrors the requisition that drove it
(different DocType, same procurement tier). Build-from lets a user
kick off assembly from any prior `Instance` whose DocType belongs
to the same **tier** (micro, standard, or complex) as the document
the user wants to assemble. It always carries the source's fill-in
values and inclusion-tag selections forward into the new assembly.
It also carries the source's ordered node list — including any
user-clause inserts and admin-clause deletes — *only when the
target DocType matches the source's*. When the target DocType
differs (still same tier), the node list is built fresh from the
target's `NodeHierarchy`, because a node list shaped for one
DocType would inject the wrong clauses into a different one (e.g.,
a requisition's clauses bleeding into a solicitation). In both
cases the user is dropped into the normal assembly flow with the
seeded state pre-loaded for editing. Pre-fill of fill-in values
from a prior instance is already wired into the assembly-time flow
(see [fill-in-assembly.md](./fill-in-assembly.md)); build-from is
the user-facing capability that *chooses* the prior instance and
carries forward the rest of the assembly state across the
source→target DocType pair within a tier.

## Scope

Covered here: the build-from entry point in the main dashboard, the
source-instance picker (the `Instance` table), the target-DocType
selector that follows source selection, how the picker treats
archived rows, the data carried forward from the source instance
(`FillinData` and `InclusionData` always; `NodeList` only when the
target DocType equals the source's), the best-effort reconciliation
pass that aligns that data against the target DocType, and how the
new `Instance` row records its provenance via `BuildFromID`. Not covered: the runtime fill-in pre-fill mechanic
itself (see [fill-in-assembly.md](./fill-in-assembly.md));
cross-tier build-from (out of scope by decision — same tier only);
revision-of-prior-edition semantics, which use the separate
`PrevEditionID` column for a related but distinct purpose (see open
questions); the seeding of tier values on existing DocType rows
(seed-data work, separate from the design).

**Pedagogical scope note.** Reconciliation between the source's
choices and the target DocType's current shape (clauses that don't
exist in the target, fill-in keys that aren't used in the target,
inclusion tags the target doesn't expose) is *discussed* in this
doc but intentionally *not implemented* with rich reconcile UI.
The implementation favors a simple "carry what's compatible, drop
what isn't, surface what's new" pass at load time, with the user
free to finish the edit in the existing assembly form. This is
consistent with the book's framing: best-effort scaffolding for the
procurement specialist, not automated correctness.

## Use case

Two scenarios, paired to make the same-DocType vs. cross-DocType
distinction concrete.

**Scenario A: another school bus requisition (same DocType).**

A procurement specialist buys school buses on a recurring basis.
For the first procurement they assembled a micro-tier requisition,
added a user clause specific to school buses, and deleted two
boilerplate clauses that didn't apply. That requisition is now an
`Instance`. For the next school bus procurement they want to start
from that prior assembly rather than from scratch — same DocType,
same structural edits, just new fill-in values for the new buy.

1. User opens `MainDashboard`. The prior school bus requisition
   row appears in the Instance table.
2. User clicks **Build from this** on that row (or clicks
   **+ New Document**, chooses *Build from existing*, and selects
   the same row in the source picker).
3. The *Build what?* step lists same-tier DocTypes; the source's
   own DocType (micro-tier requisition) is the default. The user
   confirms.
4. MAKEDOC opens `AssemblyForm` with the source's full state
   carried forward: `NodeList` exactly as the source had it (the
   user clause still in place, the two deleted boilerplate clauses
   still absent), `FillinData` pre-filling the form, and
   `InclusionData` pre-selecting any inclusion tags. Because the
   target DocType equals the source's, the reconciliation pass has
   nothing to drop or append — the banner reports "carried in
   full."
5. The user updates the fields that differ for this procurement
   (vendor, dates, quantities) and clicks **Generate Document**.
6. A new `Instance` row is written with
   `BuildFromID = <source InstanceID>` and the same `DocTypeID`
   as the source. The DOCX is delivered.

**Scenario B: build a micro-tier solicitation from a micro-tier
requisition (cross-DocType, same tier).**

1. User opens `MainDashboard` and clicks **+ New Document** above
   the Instance table.
2. A picker step asks *Start blank* or *Build from existing*. The
   user chooses **Build from existing**.
3. MAKEDOC presents the Instance table as the source picker.
   Archived `Instance` rows are shown but rendered at reduced
   opacity with an "Archived" badge; their selection control is
   disabled. Non-archived rows are selectable.
4. The user selects the prior micro-tier requisition. MAKEDOC
   reads the source's `DocTypeID`, looks up its `Tier` (micro),
   and presents a small follow-up: *Build what?* — listing the
   DocTypes in the same tier (micro-tier requisition, micro-tier
   solicitation, micro-tier PO, etc.). The source's own DocType is
   the default; the user picks micro-tier solicitation.
5. MAKEDOC opens `AssemblyForm` for the chosen target DocType
   with seeded state, reconciled against the target:
   - The ordered node list is built **fresh** from the target's
     `NodeHierarchy` — the source's `NodeList` is not carried,
     because it was shaped for a requisition and would inject
     requisition clauses into a solicitation. The user can still
     edit the new node list as usual in the assembly form.
   - The fill-in form is pre-populated from the source's
     `FillinData` for any keys the target's clauses also use; new
     keys appear as empty inputs; source-only keys are dropped
     (and surfaced in the reconciliation banner).
   - Inclusion-tag selections are pre-populated from the source's
     `InclusionData` for any tags the target DocType also exposes;
     source-only tags are dropped.
6. The user edits anything (node list, fill-in values, tag
   selections) and clicks **Generate Document**.
7. A new `Instance` row is written with
   `BuildFromID = <source InstanceID>` and the target's
   `DocTypeID`. The DOCX is delivered.

**Alternative entry: from the Instance table itself.** A row
action — "Build from this" — on each non-archived row jumps
straight to the *Build what?* step for the row's tier. Both entry
points reach the same code path.

**Acceptance:**

-   **Same-DocType build-from** (Scenario A): with no edits, the
    assembled DOCX's ordered NodeIDs, fill-in values, and inclusion
    tags match the source instance. The reconciliation banner
    reports "carried in full."
-   **Cross-DocType same-tier build-from** (Scenario B): the
    assembled DOCX for the target DocType uses the target's own
    node list (built fresh from `NodeHierarchy`), with fill-in
    values and inclusion tags carried over where compatible. The
    reconciliation banner reports what carried for fill-ins/tags
    and what was dropped or is new.
-   The new `Instance.BuildFromID` points to the source instance
    regardless of whether the source and target DocTypes match.
-   Build-from where source and target are in different tiers is
    not offered — the *Build what?* step lists only same-tier
    DocTypes.
-   Archived instances appear in the picker but cannot be
    selected.

## Key concepts

-   **Tier** — micro, standard, or complex. A property of a
    `DocType`. Build-from compatibility is defined by tier
    membership: any pair of DocTypes within the same tier is
    eligible. Carried by a new `DocType.Tier` column (see Data
    model impact).
-   **Source instance** — the prior `Instance` row whose state
    seeds the new assembly. Its DocType determines the *tier* of
    the new document; it does not have to determine the *DocType*
    of the new document.
-   **Target DocType** — the DocType the user wants to assemble,
    chosen from the same tier as the source. Defaults to the
    source's own DocType (the "build another one like this" case).
-   **Carry-forward set** — the data copied from the source into
    the new assembly. `FillinData` and `InclusionData` always
    carry. `NodeList` carries **only when the target DocType
    equals the source's DocType**; in cross-DocType build-from,
    the new node list is built fresh from the target's
    `NodeHierarchy`. The carry-forward rule for `NodeList` is
    therefore conditional on DocType identity, while the rule for
    fill-ins and inclusion tags is unconditional (with their own
    reconciliation against the target's variable set and tag set).
-   **Same-tier invariant** — build-from operates within a single
    `Tier`. A micro-tier instance can seed any micro-tier
    DocType; a standard-tier instance cannot seed a micro-tier
    DocType, even if the clauses overlap.
-   **Visible-but-disabled archived rows** — archived instances
    remain in the picker for context (so a user can see what's
    been done historically) but cannot be selected as a source.
    The Archive feature is the proper place to work with archived
    rows.
-   **Reconciliation** — the load-time pass that aligns the
    source's `FillinData` and `InclusionData` with the target
    DocType's current fill-in variable set and `InclusionTags`.
    Best-effort: keep what's compatible, drop what isn't, append
    what's new. The node list is *not* reconciled in the same
    sense — when target DocType equals source DocType, the
    source's `NodeList` is carried as-is (same clause vocabulary,
    nothing to reconcile); when they differ, the node list is
    built fresh from the target's `NodeHierarchy` (no carry to
    reconcile against). Build-from does not freeze the target
    DocType at any prior point in time.

## Data model impact

One schema change plus reuse of existing `Instance` columns.

**Schema change — new column on `DocType`:**

-   `Tier` — `TEXT` on `DocType`, values `'micro'`, `'standard'`,
    `'complex'`. Required on `Type='main'` rows. On
    `Type='attachment'` rows (DEI, HAZMAT, etc., per ADR-005), tier
    is irrelevant and the column may be NULL — attachments are
    selected indirectly via their main DocType's `InclusionTags`
    and never appear in the build-from target picker.
-   Seeding the new column on existing `Type='main'` rows is a
    one-time data task, similar in shape to the ADR-005 migration.

**Existing `Instance` columns used as-is:**

-   `BuildFromID` — already defined on `Instance` (see ADR-004 and
    [database.md](../database.md)). Set on the new instance to the
    source's `InstanceID`.
-   `FillinData`, `InclusionData` — read from the source, fed
    through the reconciliation pass, used to seed the new
    assembly, then written fresh on the new instance from the
    user's final state.
-   `NodeList` — read from the source, but only *used* to seed
    the new assembly when the target DocType equals the source's
    DocType. In the cross-DocType case the source's `NodeList`
    is dropped and the new assembly's seeded node list is built
    from the target's `NodeHierarchy` walk. The new instance's
    `NodeList` is written from the user's final state in either
    case.
-   `DocTypeID` — read from the source to look up its tier; the
    new instance's `DocTypeID` is the *target* DocType the user
    selected.
-   `Archived` (or equivalent flag) — read on each candidate row
    to decide selectability in the picker.

`PrevEditionID` is a separate column used for revision-of-prior-
edition flows and is not set by build-from.

See [database.md](../database.md) for the `Instance` and `DocType`
DDL; the `Tier` column is an addition to the latter.

## Service surface

New or extended operations in `MakeDoc.Core.Services`. A reasonable
home is a `BuildFromService`, or the surface can be folded into
`AssemblyService`; pick during implementation.

-   `IReadOnlyList<InstanceSummary> ListBuildFromCandidates()` —
    returns the rows shown in the picker. Each summary carries
    `InstanceID`, `DocTypeID`, `DocTypeName`, `Tier`, a display
    label, the creation timestamp, and an `IsArchived` flag (so
    the UI can render disabled rows and group/filter by tier).
-   `IReadOnlyList<DocTypeSummary> ListTargetDocTypes(Guid
    sourceInstanceId)` — looks up the source's tier and returns
    the `Type='main'` DocTypes in that tier, in display order.
    Used to populate the *Build what?* step.
-   `BuildFromSeed LoadSourceInstance(Guid sourceInstanceId)` —
    reads the source `Instance`, returns its `DocTypeID`, `Tier`,
    `NodeList`, `FillinData`, and `InclusionData`. The full
    source state is loaded; whether `NodeList` is then *used* to
    seed the new assembly is decided downstream in `Reconcile`.
    Throws if the source is archived (the UI should prevent this,
    but the service enforces).
-   `ReconciledSeed Reconcile(Guid targetDocTypeId, BuildFromSeed
    source)` — runs the load-time reconciliation pass and returns
    a structure carrying:
    - the seeded ordered node list. **If `targetDocTypeId ==
      source.DocTypeID`**, this is the source's `NodeList`
      carried as-is, preserving the user's structural edits
      (added user clauses, deleted boilerplate clauses).
      **Otherwise**, this is built fresh from the target's
      `NodeHierarchy` walk — the source's `NodeList` is dropped
      (and noted in the diff summary as "node list rebuilt for
      target DocType"). The cross-DocType case does not attempt
      a node-by-node intersection; the carry is all-or-nothing
      based on DocType identity.
    - the reconciled fill-in map (intersection of source keys
      with the target's variable set, plus blanks for new keys),
    - the reconciled inclusion-tag selection (intersection of
      source tags with the target's `InclusionTags`),
    - a diff summary (counts and lists of what carried, what was
      dropped, what is new) for the UI banner. In the same-
      DocType case the summary will typically read "carried in
      full"; in the cross-DocType case it reports the fill-in /
      inclusion-tag deltas plus the rebuilt-node-list note.
-   `Guid RecordInstance(... Guid? buildFromId, ...)` — already
    defined for the standard assembly flow (see
    [fill-in-assembly.md](./fill-in-assembly.md)); build-from
    passes the source `InstanceID` through.

The fill-in pre-fill itself is unchanged — it's already triggered
by `BuildFromID` per ADR-004, and the `FillinData` it pre-fills
from is whatever ends up on the seed (post-reconciliation in the
build-from case).

## UI

Two entry points reach the same flow:

-   **Picker step from `+ New Document`.** When the user clicks
    the Create button on `MainDashboard`, a small modal asks
    *Start blank* or *Build from existing*. Choosing the latter
    shows the Instance table as a source picker (filterable /
    groupable by tier and DocType). Archived rows render at
    ~50–60% opacity with an "Archived" badge and disabled
    selection. After source selection, a follow-up *Build what?*
    step lists the same-tier DocTypes; the source's own DocType
    is the default. Confirming opens `AssemblyForm` with seeded,
    reconciled state.
-   **Row action on the Instance table.** A "Build from this"
    action on each non-archived row jumps directly to the *Build
    what?* step for that row's tier, then opens `AssemblyForm`
    the same way.

`AssemblyForm` itself does not need a new mode — it accepts the
same seeded state regardless of whether the seed came from build-
from or from a fresh start. A small banner in the form ("Building
*<target DocType>* from *<source label>*") tells the user the
assembly is seeded and from what; a link reveals the
reconciliation diff (carried, dropped, new).

Add a `BuildFromPicker` subsection to [forms.md](../forms.md)
once the form exists.

## Alternatives considered

-   **Strict same-DocType only.** Rejected. The user's actual
    workflow includes "build the solicitation from the
    requisition that drove it," which crosses DocTypes within a
    tier. Restricting to same-DocType would push that workflow
    back into copy/paste territory.
-   **Fully open cross-DocType build-from** (any source, any
    target). Rejected. Without a compatibility scope, the
    reconciliation surface gets unbounded — fill-in keys and
    node lists between, say, a complex-tier solicitation and a
    micro-tier requisition have little reason to align, and the
    "carry-forward" promise becomes hollow. Tier scoping picks
    the natural unit where overlap is meaningful.
-   **Encode tier inside the DocType name** (e.g., parse "micro-
    tier solicitation" string-wise) instead of adding a `Tier`
    column. Rejected. Names are for humans; the data model should
    declare the relationship explicitly. A column also leaves
    room for tier metadata (display order, descriptive label) if
    the book wants it later.
-   **Use a Tier table with a foreign key from `DocType`** instead
    of a `TEXT` enum-style column. Reasonable; deferred. The
    enum-style column matches the precedent set by
    `DocType.Type` (ADR-005) and keeps the seed migration short.
    Promote to a table if tiers grow attributes beyond a name.
-   **Hide archived rows entirely from the picker.** Rejected.
    Showing archived rows gives users historical context — they
    can see lineage without leaving the picker. Disabling rather
    than hiding preserves that context while preventing
    accidental forks from deprecated instances.
-   **Freeze the target DocType at the source's assembly time**
    (replay the source's NodeHierarchy as it was). Rejected.
    Build-from always assembles against the *current* target
    DocType. A historian who wants the original DOCX can fetch
    the source's archived output directly; build-from is a
    starting point for a new document, not a re-render of an old
    one.
-   **Pre-fill only fill-ins, not the node list** (a "fill-in
    template" capability, regardless of source/target DocType).
    Rejected as a blanket rule — when source and target are the
    same DocType, dropping the source's `NodeList` would discard
    the user's structural edits (added user clauses, deleted
    boilerplate), which is exactly the convenience that build-
    from is supposed to provide for recurring procurements (see
    Scenario A — school bus requisitions). Adopted *only* for
    the cross-DocType case, where carrying a node list shaped
    for one DocType into a different one would inject the wrong
    clauses (the requisition→solicitation case in Scenario B).
    The conditional rule — same-DocType carries `NodeList`,
    cross-DocType does not — splits the difference.
-   **Use `PrevEditionID` for the build-from link.**
    Rejected. `PrevEditionID` carries the meaning "this is a
    revision of that earlier document" (same logical document,
    new edition). `BuildFromID` carries the meaning "this is a
    new document shaped like that earlier one — possibly of a
    different DocType in the same tier." Conflating them would
    lose the distinction at audit time.
-   **Single Create button that always opens the picker step**
    (vs. a separate "Build from existing" CTA). Accepted as the
    primary entry — keeps the dashboard uncluttered and lets the
    branching live in one place. The Instance-table row action
    is a complementary entry, not a competing one.

## Open questions

-   **`Tier` shape.** Free-text column with a check constraint, a
    small lookup table, or an enum in code with a string column?
    The doc above assumes the simplest option; revisit if tiers
    pick up attributes (display order, descriptions, allowed
    DocType counts) the system should know about.
-   **Reconciliation surface.** When the pass drops or appends
    nodes / fill-in keys / inclusion tags, how loud is the
    notice? A banner with counts ("3 nodes carried, 1 dropped,
    2 new; 4 fill-ins carried, 1 dropped, 2 new") is the
    minimum. A diff dialog is more informative but more form-
    machinery; given the pedagogical scope, banner-plus-link is
    probably right.
-   **Fill-in key alignment across DocTypes.** Today, fill-in
    identity is the SDT's visible text (ADR-004). Cross-DocType
    build-from leans on author discipline — `contractor_name`
    means the same thing in a requisition and a solicitation
    only because authors named it consistently. Worth flagging
    in the book even though the system itself doesn't enforce
    it.
-   **Default target DocType.** Today: the source's own DocType.
    Alternative: a per-tier "natural successor" map (requisition
    → solicitation, solicitation → award). Probably premature;
    raise if users ask.
-   **Tier on attachments.** Attachments (DEI, HAZMAT, etc.,
    per ADR-005) are `Type='attachment'` and never appear in
    the build-from target picker, so their `Tier` is left NULL
    above. Confirm this is the intended treatment, or whether
    attachments should be tier-scoped for any other reason.
-   **Picker defaults.** Sort order (most recent first is the
    obvious default), grouping (by tier, then by DocType?),
    filters (by date, by DocType, by tier, by author if/when
    authorship is tracked).
-   **Lineage visibility.** `BuildFromID` chains form an instance
    lineage that may now cross DocTypes within a tier. Should
    the Instance table surface lineage (a "built from" column or
    a small chevron expanding ancestors)? Useful for analytics;
    not needed for build-from itself.
-   **Allow build-from of an archived instance after explicit
    unarchive?** The current rule disables selection. If a user
    really needs an archived row as a source, they would
    unarchive it first via the Archive feature, then it becomes
    selectable. Confirm this is the expected workflow rather
    than offering an inline "use anyway" override.
-   **Audit of carried-forward state.** When the user edits the
    seeded node list or fill-in values before generating, do we
    record the diff against the source on the new `Instance`,
    or only the final state? Final-state-only is simpler and
    matches the rest of the system; reconciliation diffs would
    be inferable from joining source and new on `BuildFromID`.

## Related

-   [decisions.md](../decisions.md) — ADR-004 (the existing pre-
    fill behavior triggered by `BuildFromID`), ADR-005
    (inclusion tags / `InclusionData`, also carried forward by
    build-from), and **ADR-007** (this feature, the same-tier
    compatibility rule, the `DocType.Tier` column, and the
    revised conditional carry-forward rule for `NodeList`).
-   [fill-in-assembly.md](./fill-in-assembly.md) — the runtime
    pre-fill mechanic that build-from leans on.
-   [assemble-document.md](./assemble-document.md) — the
    assembly flow that build-from feeds into.
-   [architecture.md](../architecture.md) — build-from as a
    top-level MAKEDOC capability.
-   [services.md](../services.md) — where the build-from
    service methods land.
-   [forms.md](../forms.md) — `MainDashboard` Create button, the
    picker step, the *Build what?* step, the row action on the
    Instance table.
-   [database.md](../database.md) — `Instance.BuildFromID`,
    `NodeList`, `FillinData`, `InclusionData`, archive flag, and
    the new `DocType.Tier` column.
