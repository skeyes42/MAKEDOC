# Document Structure in the MAKEDOC system
## Parts of a MAKEDOC Document
A document consists of an order set of **clauses** (See clause-structure.md for more detail.)

## Clause Ordering
The first clause of a MAKEDOC document is a NodeType (defined the Node table) **headernode**. This node (i.e. DOCX file) typically contains a header for the document introducing
things like the procuring agency, date, phase of procurement (requisition, solicitation or award), ID information (e.g. requisition number). The second and subsequent nodes in the 
**node list** are of NodeType = **clause**. Like the the clause nodes, the headernode can have fill-ins. (See clause-structure.md for details on fill-ins.)

## Assembly Process Overview
The assembly process basically does the following:
- Create a new DOCX file form the **template** document associated with the particular doctype. (Like headernodes and clause nodes, the templates are stored as docx file in the
Node table.)
- Append the headernode
- Append all of the clause nodes
- Resolve any fill-ins.

## Instance Table
For each assembled document there exists a row in the **Instance** table. This entry contains information about a particular assembled document:
- Clause list
- Fill-in data used to satisfy the fill-in fields found in the clauses
- Date create
- Etc.

## Build-from Process
The system allows the user to build a new Instance from an existing Instance.


