# MAKEDOC Document Restrictions
This document outlines the general structure of documents that MAKEDOC can build.

1. MAKEDOC assumes that clause nodes contain text (**content**) that is self-referencing. That is each clause node contains text that identifies the clause -- by title.
2. Clause nodes must have a plain-text fill-in {SecNo} to allow the system to consecutively number the sections starting from 1.
3. Clause content and section numbering are paired  1-to-1. That is each section may only contain one clause. If a section has multiple clauses, the clause texts of the different constituent clauses should be combined.
4. MAKEDOC does not support sub-sectioning. 
5. MAKEDOC allows special clauses (groups of clauses know as **clause groups**) to be added at a location in the document chosen by the user. Examples of special clause groups would be for DEI, HAZMAT, etc.
6. Special clause groups are freely configured by the customer.

