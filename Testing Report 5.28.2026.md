# Testing Report — MAKEDOC (5.28.2026)

## All Items Resolved

All issues identified in the initial test pass (5.27.2026) have been fixed and verified.

---

## Fixed

### {SecNo} not populated
**Root cause:** `SubstitutePlainText` scanned individual `w:t` elements for the literal string `{SecNo}`. Word had split the tag across three separate `w:t` elements (`{` | `SecNo` | `}`) in several micro-tier solicitation clauses, so no match was ever found.

**Fix:** `FillinService.SubstitutePlainText` was rewritten to operate at the paragraph level. It now concatenates all `w:t` values within each `w:p`, finds the pattern in the combined text, and maps the replacement back across whichever elements span the match. Tested with micro-tier solicitation — section numbers appear correctly in the assembled document.

### Fill-ins in header node not working
**Root cause:** The assembly substitution loop in `AssemblyForm.OnGenerateClicked` applied `SubstituteFillins` only to nodes of type `Clause`. Header nodes were correctly scanned (their fill-in variable names appeared in the Fill-in form) but the substituted values were never written back into the header node blob before assembly.

**Fix:** An `else if` branch for `NodeTypes.HeaderNode` was added to the substitution loop. Header nodes now receive `SubstituteFillins` but not `SubstitutePlainText` (section numbering does not apply to header nodes). Tested — header fill-ins appear correctly in assembled documents.

---

## Implemented and Tested

### Build-to solicitation
- Dashboard context menu shows **Build to solicitation...** when a requisition is selected (hidden for all other document types and for archived documents).
- User selects the target solicitation DocType from a picker. Tier matching is the user's responsibility.
- Assembly form opens with the target DocType's canonical node list pre-loaded.
- Fill-in values from the source requisition are pre-populated in the Fill-in form, matched by tag name.
- New Instance row records the source requisition as `BuildFromID`.
- Tested: micro requisition → micro solicitation. Fill-in carry-forward worked correctly.

### Build-to award
- Dashboard context menu shows **Build to award...** when a solicitation (or amended solicitation) is selected.
- Same flow as build-to solicitation, targeting award DocTypes.
- Tested: micro solicitation → micro award. Fill-in carry-forward worked correctly.

---

## Remaining Work

- Fill-in name consistency review for **standard** and **complex** tiers (micro tier clauses have been reviewed and aligned).
- Build-to solicitation and Build-to award have only been tested at the micro tier. Standard and complex tier testing to follow once clause fill-in names are verified for those tiers.
