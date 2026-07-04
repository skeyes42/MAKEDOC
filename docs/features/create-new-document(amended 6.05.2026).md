# Create new document from canonical document and from build-from document

# Subject Matter Update Regarding build from/build to
## Procurement Flow
The flow of a procurement is, typically, from requisition to solicitation to award. Documents are created at each phase. These different documents share information that is provided by the procurement specialist. The information in a document is carried in the document's **fill-in** variables. Information at requisition time (e.g. requisition number, name of the requestor, etc.) passes to the solicitation phase whose documents may include some or all of the requisition information, as well as, new information collected during solicitation. Finally, the award phase is informed by information from the solicitation phase. Passing information from one procurement phase to the next is done using the **build from** process. Build from lets the procurement specialist create a solicitation document from its associated requisition document, and an award document from the associated solicitation document. Note: requisitions are usually created from scratch -- from boilerplate ("canonical") clauses. (In some systems, requisition information comes from an associated **shopping cart**, as well as, from the requisitioner.) The build from process can be seen as a convenience to the procurement specialist. Build from allows the specialist to avoid having to re-provide the same information over and over. For example, if requisition number is captured in the requisition document, then the building of a solicitation from the requisition automatically passes this data forward preventing the need for re-entry. When you multiply this example by one or two orders of magnitude, you can readily see the labor savings provided by build from.

**Note**
1. The converse of a build from is a **build to**. One way of looking at, say, building a solicitation from a requisition, is refer to the process as "building the requisition to a solicitation". 
2. The procurement process is <u>one-way</u>. Awards are only build from solicitations and solicitations are only built from requisitions.
3. Build from always occurs within the same **tier** where tier is a category based on the dollar amount of the procurement, e.g. micro, standard and complex for low, medium and high dollar amounts. That is, you never see a standard solicitation being built from a micro requisition.

Another wrinkle of the procurement process occurs when changes must be made to **published** (i.e. approved) solicitation and award documents. It's not uncommon for requirements to change during the solicitation phase which can mean changes to the solicitation document, and often changes to the information contained in that document. The same scenario is true for the award phase. Change made to a solicitation document is called  an **amendment**, and change made to an award document is call a **modification**. For example, for solicitations, an amendment is simply an edited copy of the original, published solicitation, and the same is true for awards. The build from process (above) can handle these changes. The approach is simply to allow build from to build a new document from the current document -- a copy. It is the copy of the published solicitation that is changed -- not the original solicitation, thereby preserving the <mark>paper trail</mark>. 


## Definition of a Node Group

A **node group** is a collection of nodes organized as an adjacency list in the Node table, rooted at a header node. Node groups behave like document types in several ways:

- Each node group has an associated template and a header node.
- A node group can be assembled like a document.
- A node group is identified by an **inclusion tag**.

Inclusion tags are defined in the **DocType** table. Each DocType may have a non-empty `InclusionTag` field. This field holds a JSON array; each element has the shape:

```json
{ "tagName": "<display label>", "docTypeId": "<DocTypeID>" }
```

Where:

- `tagName` is the text displayed to the user as a selection in the Assembly form.
- `docTypeId` is the **DocTypeID** in the DocType table that roots the node group's adjacency list.

## Pre-conditions for Building a new Document from Canonical Clauses
Line item information is central to building a new document from canonical clauses:

- Line item totals determine the **tier** category (dollar amount) which determines which clause library is used to build the document. In MAKEDOC there are three tiers:
    - **micro tier** is for low dollar procurements
    - **std tier** is for medium dollar procurements (between low and high dollar categories)
    - **cmplx tier** is the high dollar procurments (dollar amount greater than the std tier)
    - These tier categories are configured in the makedoc.config.json file

{
  "DatabasePath":                "C:/Users/skeye/BOOK2/MAKEDOC/db/MAKEDOC.db",
  "SeedPath":                    "C:/Users/skeye/BOOK2/MAKEDOC/db/seed",
  "SQLPath":                     "C:/Users/skeye/BOOK2/MAKEDOC/db/sql",
  "PS1Path":                     "C:/Users/skeye/BOOK2/MAKEDOC/db/scripts",
  "PlumberBaseUrl":              "http://localhost:8000",
  "ClausesRoot":                 "C:/Users/skeye/BOOK2/MAKEDOC/docs/Clauses and assembled documents/clauses",
  "assembledDocumentsDirectory": "C:/temp/assembled_documents",
  "LogLevel":                    "Info",
  "TierCategories":              [
    {"micro" : { "min": 0,  "max": 10000} },
    {"std": { "min": 10001, "max": 100000} },
    {"cmplx": { "min": 100001, "max": null} }
  ]
}


- Line items also indicate, based on the nature of the goods purchased, whether certain special clauses are required. For example if a line item is for gasoline, then the **clause group** HAZMAT would be included. (This is a manual selection on the part of the procurement specialist -- not an automatic feature.)

To implement this pre-conditioning:

- The context menu of the **Tools** option of the Dashboard form is modified to add a **line items** option.
- Before the line item information is collected, the **assembly** option of the Tools options is gray-ed out. This ensure that line item information is available at "generate document" time.
- Choosing line items from the Tools options open the Lineitem form. This form collects line item information, including for each line item:
    - Description of the line item - a short text describing the good/service
    - NAICS - the product type code
    - Quantity - the quantity of units to buy
    - Unit - the unit of measure of the line item, e.g. EA for each 
    - Price - the cost of each unit
    - Extended Price - the price extension: quantity * price

**Note**

- The line item number is supplied by the system numbering from 1 to the count of line items.
- The system also computes the **Total Price** which is the sum of the extended prices.
- Once the total price is computed, the system uses this tier information to determine the tier -- based on the config information.

- Once the line item information is collected from the user. The system presents the user with a choice of the procurement phase (requisition, solicitation, award).
- The phase information combined with the tier information determines the **document type**.
- The document type information is used to select the appropriate clause library. The appropriate clauses (used to build the **clause list**) are identified in the **NodeHierarchy** table.

At this point the system has all of the information it needs to successfully move the the **Generate Document** part of document assembly.

## How Line Item Information is Represented in the Assembled Document

- During the fill-in process (described below), various fill-ins are filled in. There are two types of fill-ins:
    - Plain text fill-ins, i.e. fill-ins identified in the DOCX text. These fill-ins are phrases formatted: {\<plain text fill-in type\>}
    - SDT fill-ins which are identified using named and title SDT controls.

There are two types of plain text fill-ins:
- {SecNo} -- a fill-in for the section number of a clause. Numbered consecutively (from 1) throughout the entire document
- {lineitem} -- this fill-in tells the system to do the following:
    - Convert the line item information into a DOCX representation
    - Also include the total price

## Shared Generate Document procedure

Both creation flows below converge on the same steps once the user clicks **Generate Document**. These steps run in this order:

1. System collects all fill-in tags found in clauses in the **transient clause list**.
2. System opens a modal **Fill-in form** to collect data for the fill-ins. This form shows fill-ins found in both the clause nodes and the header nodes.
3. User provides a title for the document in the Fill-in form.
4. User provides fill-in data and closes the Fill-in form.
5. For every clause in the transient clause list that has been edited, system writes a new user clause (UC-\<short guid\>) to the Node table. A new UC- row is always created, even when the source clause was itself a UC- clause — UC- rows are never updated in place.
6. System creates a new row in the **Instance table** from the transient node list and transient clause list (now referencing the newly written UC- node IDs).
7. During document assembly:
    - System replaces the fill-in tags in the transient clauses with the data collected from the Fill-in form.
    - System establishes a **section number count**. This integer is used by the system to fill-in the {SecNo} plain text fill-in. Starting from 1, the system fills in each {SecNo} tag, incrementing by 1 as each clause node in the assembly is encountered. 
    - The {lineitem} plain text tags are replace with the DOCX consisting the the line item table, plus total price.
8. System opens the **file-save dialog**.
9. User saves the newly assembled document.


If the user closes the form without clicking **Generate Document**, system prompts: "Click on Generate Document to keep changes." If the user confirms the dismissal, the transient lists are discarded and the form closes.

## Creating a document starting with canonical node list

- User selects a Doc Type in the Assembly form and receives a display of canonical nodes (from the NodeHierarchy table).
- System makes a mutable copy of the node list: the **transient node list**.
- System makes a mutable list of copies of all canonical clauses: the **transient clause list**.
- User manages the transient node list by picking options from the context menu:
    - **Insert User Clause** — System opens a file-open dialog; the selected file is inserted above the current clause. A user node is added to the transient node list, and the DOCX of the new clause is added to the transient clause list.
    - **Delete Clause** — System deletes the selected clause. Transient node list is updated.
    - **Edit Clause** — System opens a Word session in a dialog; user edits the clause. The edited clause is saved in the transient clause list.
    - **Move Clause** — System moves the clause under cursor control. Transient node list is updated to reflect the move.
    - **Browse Clause** — System opens a read-only Word session displaying the selected clause. Neither transient list is updated.
    - **Reset Clause List** — System restores the canonical list from the NodeHierarchy. The transient node list and transient clause list are restored to their original states.
    - **Insert Special Clauses** — System pops up a list of inclusion tags available for the doc type. If the user selects an inclusion tag, all nodes in the corresponding node group are inserted at the cursor location, preserving the group's adjacency list structure. These nodes are not added to the transient clause list since no edits to node-group clauses are allowed.
- When the user clicks **Generate Document**, the system runs the [Shared Generate Document procedure](#shared-generate-document-procedure) above.

<div style="page-break-before: always;"></div>

## Creating a document via build-from

- User selects an existing document (instance) from the list in the Dashboard form.
- User manages the selected document from the context menu:

    - **Build-from selected document**
    - **Build-to solicitation**
    - **Build-to award**    
    - **View document**
    - **Archive document**
    - **Un-archive document**

- If **View document** is selected, system re-assembles the source document and presents it in a read-only DOCX editor window.
- If **Build-from selected document** is selected, system does the following:
    - System opens the Build-from form, which displays the node list from the source document.
    - System makes a transient copy of the source node list: the **transient node list**.
    - System makes a list of copies of all clauses from the source document: the **transient clause list**.
    - User manages the transient node list using the same context menu options described above for the canonical flow.
- If **Archive document** is selected, then system sets the IsArchived field in the Instance table to 1, and updates the Dashboard form to reflect this.
- If **Un-archive document** is selected, then system sets the IsArchived field in the Instance table to 0, and updates the Dashboard form to reflect this.
- If **Build-to solicitation** is selected, then the system builds a new document from a source requisition document to a target solicitation document within the same tier. That is for example, if the source document is a micro-requisition, then the target of the build-to is a micro-solicitation. As with build-from, the transient node and clauses lists are managed accordingly.
- If **Build-to award** is selected, then the system builds a new document from a source solicitation to a target award document in the same tier.
- Once the new document is built, the system puts up a modal line item form pre-populated with the selected document's line items.
- The user can view the line items and, perhaps, change the line items.
- Once the modal dialog is closed, the line items in the form (changed or unchanged) are used to upsert line item information of the new document.


**Note**
If changes to the line items cause the total amount of the procurement are such that it no longer fit it's original dollar-amount category as defined in the config file, then the system proceeds to build the new document in the <u> same tier as the original </u>. The system can put up a message letting the user know this has happened. The general advice in the book will be to act on this message by re-doing the requisition steps in the new tier.

**Note**
System prevents user from doing a build from if IsArchived flag is 1 (true). To build from an archived document, the user must first un-archive it.

- When the user clicks **Generate Document**, the system runs the [Shared Generate Document procedure](#shared-generate-document-procedure). The new Instance row additionally inherits applicable metadata (e.g., source-document reference) from the source document.