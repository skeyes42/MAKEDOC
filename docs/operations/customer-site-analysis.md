# Customer Site Analysis

## Steps for analysis

This site analysis is performed before **MAKEDOC** is implemented at an installation. (I followed this process when developing the MAKEDOC.db database.) Here is an outline of the steps to follow in the site analysis.

1. It is assumed that the customers procurement process follows the traditional cycle: requisition \-\> solicitation \-\> award. Find out what the customer names for these different phases are and establish a customer-related name for each **phase**.

2. It is assumed that the customers procurement process involves different dollar amounts. Find out what the customer names for these different dollar amounts and establish a customer-related name for each **tier**.

3. With the tier and phase naming in place, you now have a 2 x 2 table of **categories**. For example, if the tiers are micro, standard and complex and the phases are requisition, solicitation and award, then the tables of categories contains 9 cells.

4. In additional to the tier/phase categories, most customers have sets of clauses (**clause groups**) that are sometimes included in procurement documents for special situations. Typically these **special clauses** are included independent of tier and phase.  For example, some procurements are designate as DEI, some procurements have hazardous materials, etc. Get a list of these sets of special clauses an establish a customer-related name for each **special clause group**.

5. Gather actual assembled procurement documents and bin them into the different categories.

6. Procurement documents generally have a heading for the document. Usually the heading indicates the tier and phase associated with the document. For each category, establish a header clause that identifies the general parameters (tier and phase) of the document.

7. Gather the customer special clauses and bin them into the different **special clause groups**. Establish a customer-related name for each special clause group, e.g. DEI, HAZMAT, etc.

8. Within in a category (tier/phase), look at the assembled documents and pull out the individual clauses and their titles. Establish a naming convention for each of the unique clauses, the **claude ID**. While identifying the clauses (by clause ID), note the clause IDs of the clause just before the current clause in question and the id of the clause immediately following the current clause. In the context of a given clause, these are the **parent clause** and the **child clause**. The paren /child information within a category is collectively referred to as the **node hierarchy** data of the category.

9. Within a special clauses group, identify the individual clauses and their  . Name each clause uniquely, clause ID.

10. **Regular clauses** (i.e. clauses that are not special clauses) often have fill-in opportunities where the customer can supply data. For example, a requisition usually has a unique identifying requisition number. For each regular clause found, collect the list of **fill-ins**. Assign each fill-in a name. 

**Note**
A particular fill-in maybe used in several of the documents generated the procurement cycle. For example, the solicitation document my refer to the requisition number which originally showed up in the requisition document.

## Using the results of analysis

Once the information outlined above has been collected and named it can be used to build the content of the MAKEDOC database.

- The **categories** become the document types. These are represented in the Node table as **DocTypeID**. This table has additional metadata:
    - **Inclusion tags** come from the list of **special clause groups**
    - **HeaderNodeID** are the clause IDs of the header clauses.
    
- The **clause ID** becomes the **NodeID** in the Node table. The title of the clause becomes the **Title** field in the Node table. The actual DOCX text of the clause is the **Content** field in the Node table.

- The **node hierarchy** information, which indicates the ordering of clauses in a category/document type is used to populate the fields **ParentNodeID** and **ChildNodeID** fields of the NodeHierarchy table.

- As discussed above, a clause has a title. This title becomes a **section designator**. A section designator is a piece of DOCX text that identifies the clause and contains a plain-text fill-in called the **section number**. If the title of the clause is say "DELIVERABLES", then the section designator would be:

**SECTION {SecNo} — DELIVERABLES**

At assembly-time, the system provides that sequential numbers (from 1) that allow the sections to be consecutively numbered.

- In the MAKEDOC system, both the fill-ins and the section numbers live within the texts of the various clauses. That is, there are no separate tables to track this information.



