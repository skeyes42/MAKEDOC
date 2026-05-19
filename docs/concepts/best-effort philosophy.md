# MAKEDOC Best-Effort Philosophy
MAKEDOC is a system to assemble and populate **structured documents**. But first some definitions.

## Definition of a structured document
In the MAKEDOC system, a structure document consists of the following:
- **header node** (e.g. a document title) followed by one or more **clause nodes**. The collection of clause nodes is ordered and referred to as the **clause list**.
- **clause node** is a self-referencing DOCX file that contains:
    - **section designator** — a line of text formatted as "SECTION {SecNo} — \< title of the clause\>" where:
        - **{SecNo}** is a plain-text fill-in that represents the next section number. Each clause has its own unique section number. Section numbers are managed by the MAKEDOC system starting at 1 and increase sequentially throughout the document
    - **clause content** — a DOCX file (of any size) that represents the content of the clause.

In BNF notation:

\<document\> ::= \<header node\> <\clauses list\>

\<clause node\> ::= \<section designator\> \<clause content\>

\<clause list\> ::= \<clause node\> \<clause node\> \<clause node\> ...

## The Concept of "Best-Effort"
The MAKEDOC system is designed to support the domain of public sector state\/local government. To provide a more realistic look-and-feel, MAKEDOC is designed to support state procurement for the fictitious state of **Northlandia**. The documents and clauses that are used in the MAKEDOC system are drawn from actual examples of state procurement documents. 
When a Northlandia procurement specialist is charged with the task of creating a procurement (e.g. a contract for a complex award), he/she is faced with selecting clauses from a library of, perhaps, hundreds of candidate clauses. Some clauses would be inappropriate for large dollar amount (the **tier**) and some clauses would be inappropriate due to their
connection with a **phase** of procurement, e.g requisition clauses don't work in a contract document. These restrictions narrow the possible list of clauses that could go into a complex award (contract) document -- so the job of building the contract is somewhat easier, although still complicated by issues of clause ordering, special clauses, fill-ins, etc. MAKEDOC's job to to make this process less labor-intensive and more consistent. What MAKEDOC produces is a "first draft" of the contract. MAKEDOC, in a sense, is a convenience for the contract specialist. Once the first-draft is available, MAKEDOC supports adding, deleting, editing clauses to the document from the first draft to produce the final document. Also, instead of starting with a first-draft, MAKEDOC lets the procurement specialist make the "next" contract from some previous contract in a process referred to a **build from**.

The end product, the final contract, (no matter how it was produced -- either with or without MAKEDOC), is the <u> responsibility of the procurement specialist </u>. To repeat, the goal of using MAKEDOC is that the final products are produced faster and more consistently than by construction by hand.

If there's a gap between the document produced by MAKEDOC and the desired final document, then this becomes a MAKEDOC maintenance issue. Maintenance of the MAKEDOC.db database can tune the output to move closer to better final output documents.