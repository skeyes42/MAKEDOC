# ADR-NNNN: Merge Section Text into Clause Text and Remove Section/Subsection NodeTypes

- **Status:** Proposed
- **Date:** 2026-05-11
- **Supersedes:** _[fill in any prior ADRs that established the Section/Subsection NodeTypes or the hard-coded section-numbering convention]_

## Context

In the current MAKEDOC node structure, each clause in a document instance is represented by two nodes:

1. A `Section` node containing text like `SECTION 3 — Deliverables`, with the section number **hard-coded into the text**.
2. A `Clause` node containing the body of the clause.

This structure has two recurring problems:

**Hard-coded section numbers.** Because the section number is baked into the Section node's text, inserting or removing a clause requires editing every downstream Section node's text to keep numbering sequential. This is fragile, easy to miss, and has been a source of incorrect section numbering in generated documents.

**Two-node insertion for user clauses.** Adding a user-supplied clause requires the caller to add both a Section node and a Clause node, in the right order, with a correctly computed section number. The user-clause path is therefore significantly more cumbersome than a single-node insert would be.

Separately, the system currently supports a `Subsection` NodeType, used by two of the nine document types. Subsections add complexity to the document model and the rendering pipeline without expressing anything that couldn't be expressed within a clause body. They have not earned their nodehood.

MAKEDOC targets state/local procurement, where (unlike federal FAR documents) section numbering can be arbitrary as long as it is sequential. This gives us the freedom to renumber dynamically.

## Decision

We will restructure the document model as follows:

1. **Merge section text into clause text.** Each logical clause is represented by a single `Clause` node whose body contains both the section heading and the clause body. Example:

   ```
   Section {SectionNumber} — Deliverables

   This is the text of the clause. It has several lines.
   The text continues with another line.
   Here is the last line.
   ```

2. **Use `{SectionNumber}` as a fill-in placeholder.** Section numbers are no longer stored in node text. At assembly time, the system supplies sequential section numbers via the `{SectionNumber}` fill-in. (This is a plain fill-in, not an SDT.)

3. **Remove `NodeType = 'Section'`.** Section nodes no longer exist in the node hierarchy.

4. **Remove `NodeType = 'Subsection'`.** Subsections are no longer supported by MAKEDOC. The two document types currently using subsections will be rewritten to express the same content without them.

5. **Re-seed `NodeHierarchy`** to reflect the new structure.

The result: one node per logical clause, with dynamic section numbering and a single insertion path for both built-in and user-supplied clauses.

## Alternatives Considered

### Alternative A — Keep the current structure
Rejected. The hard-coded section numbers and two-node user-clause insertion are ongoing pain points. Doing nothing preserves both indefinitely.

### Alternative B — Hybrid: keep Section nodes for built-in clauses, merge for user clauses
Considered seriously. Built-in clauses would retain their separate `Section` + `Clause` structure (with Section nodes updated to use the `{SectionNumber}` fill-in), while user-supplied clauses would use the merged single-node form.

Rejected because:

- The migration effort it avoids is small. Every existing Section node already needs to be edited to add the fill-in; the marginal cost of also folding the heading into the Clause node is minor.
- It creates two structural patterns that have to coexist permanently. Every piece of code that walks the node tree would have to branch on whether a clause is built-in or user-supplied.
- Built-in and user clauses would have asymmetric representations in the database, complicating any future operation that treats them uniformly (display, export, promotion of a user clause into the built-in library, etc.).

The hybrid is reasonable as a stepping stone but not as a destination. Going directly to the merged structure is cheaper in total cost.

## Consequences

### Positive
- Single-node insertion for both built-in and user clauses.
- Section numbering is computed at assembly time. Inserting or removing a clause no longer requires editing the text of other nodes.
- One uniform node shape across the document model. `Section` and `Subsection` NodeTypes are removed entirely.
- Simpler mental model, simpler documentation, simpler tree-walking code.

### Tradeoffs / costs
- **One-time migration.** Every existing `Clause` node must be updated to include the (formerly separate) section heading text plus the `{SectionNumber}` fill-in. Based on the most recent comparable migration, this is expected to take roughly 1.5 hours of focused work.
- The two document types that currently use subsections must be rewritten to remove them.
- The `NodeHierarchy` seed must be updated.

### Product limitation (intentional)
MAKEDOC will not support subsections going forward. This is a deliberate simplification and will be documented as an explicit product limitation. Users needing subsection-style organization should express it within the clause body.

### Out of scope
Federal FAR-based document generation, where section numbering is regulatorily fixed, is not affected by this decision.

## Implementation Notes

- Update the `NodeHierarchy` seed to remove `Section` and `Subsection` entries.
- Migrate existing `Clause` node text to prepend the section heading and `{SectionNumber}` fill-in.
- Remove `Section` and `Subsection` from the NodeType enumeration (or equivalent) and any code paths that branch on them.
- Rewrite the two document types currently using subsections.
- Update the assembly pipeline to substitute `{SectionNumber}` with sequential integers.
- Update user-facing documentation (and the in-progress book) to reflect the no-subsections constraint and the merged clause structure.