# Potential Holes in the Makedoc app
This document is used to discuss things that the MAKEDOC app doesn't do. Situations that is doesn't handle, but, in the real world, come up.

## Regarding Maintenace
Right now the way I see Maintenance activities:
- Nodes can be edited. That is a selected node (clause) can be downloaded, edited, then uploaded.

Here some things we're not addressing and why:
-  Right now, we're not going to handle deleting boilerplate nodes (NL-\<nnnn\>). The reasons why:
-- The deleted node may be present in some Instance's NodeList field.
-- The deleted node also shows up in the NodeHierarchy table.
- Nodes cannot be moved. The reason for this:
-- Each clause node has an associated section/subsection node. When assembling the document, the section clause is rendered right before the clause. If you move the clause node
without also moving its section node, it's going to be confusing. If you deleted the clause node, aside from the issues just mentioned, you have an empty section, which would
be OK. There are several ways to handle this: the easiest way would be to combine the clause's section information in the text of the clause itself. That is, do away with the "section" node type. This gets a little complicated with subsections. Another approach would be to support some level of association between the clause and its section -- some
way to "pair" node (section and clause) so that when one of the pair is moved or deleted, so is the other. Yet another way to handle this is to depend on the section/clause
relationship found implicitly in the NodeHierarchy table.
- The NodeHierarchy table is also not maintained. The reason being it messes up the clause node rendering in the assembly process. One possible way to handle this would be to say
that doc types cannot be deleted. If you want to reorder the section/clause ordering in the NodeHierarchy table would be to create a new doc type -- a kind of doc type versioning.

I don't think this is a blocker for the MAKEDOC application, but it certainly requires some discussion in the book. We never claimed that the MAKEDOC app is production ready.
It was built to illustrate/highlight core concerns/issues/concepts that come up in any document generation system.

The old Document Builder dealt with these issues. The old Document builder had an explicit notion of a "section". A document for Document Builder was a collection of sections.
These sections, in turn, had clauses. To bend the MAKEDOC architecture to fit this model would, probably, mean a table re-design and an abandoment of a node-type database of
adjacency lists.