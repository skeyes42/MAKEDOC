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

## Shared Generate Document procedure

Both creation flows below converge on the same steps once the user clicks **Generate Document**. These steps run in this order:

1. System collects all fill-in tags found in clauses in the **transient clause list**.
2. System opens a modal **Fill-in form** to collect data for the fill-ins. This form shows fill-ins found in both the clause nodes and the header nodes.
3. User provides a title for the document in the Fill-in form.
4. User provides fill-in data and closes the Fill-in form.
5. For every clause in the transient clause list that has been edited, system writes a new user clause (UC-\<short guid\>) to the Node table. A new UC- row is always created, even when the source clause was itself a UC- clause — UC- rows are never updated in place.
6. System creates a new row in the **Instance table** from the transient node list and transient clause list (now referencing the newly written UC- node IDs).
7. System replaces the fill-in tags in the transient clauses with the data collected from the Fill-in form.
8. System assembles the document.
9. System opens the **file-save dialog**.
10. User saves the newly assembled document.
11. During document assembly, the system establishes a **section number count**. This integer is used by the system to fill-in the {SecNo} plain text fill-in. Starting from 1, the system fills in each {SecNo} tag, incrementing by 1 as each clause node in the assembly is encountered.

> **Implementation status (as of 5.28.2026):** All steps fully implemented and tested. Step 11 ({SecNo} population) is working — including clauses where Word split the `{SecNo}` tag across multiple runs. Header node fill-ins (step 2) are collected and substituted correctly.

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

> **Implementation status (as of 5.28.2026):** All context menu options fully implemented and tested, including Build-to solicitation and Build-to award. Fill-in carry-forward across document types verified for the micro tier (req → sol → awd).

### Context-sensitivity of menu items

The Dashboard context menu enforces the following rules based on the document type of the selected instance:

| Selected document type | Items enabled |
|---|---|
| Requisition (any tier) | Build-from selected document, Build-to solicitation |
| Solicitation or amended solicitation (any tier) | Build-from selected document, Build-to award |
| Award or modification (any tier) | Build-from selected document |

Items that do not apply to the selected document type are **disabled** (greyed out) in the context menu. The system determines document type from the DocTypeID stored in the Instance table row for the selected document.

It is the user's responsibility to select a target DocType in the same tier as the source document. The system does not enforce tier matching.

### View document

If **View document** is selected, system re-assembles the source document and presents it in a read-only DOCX editor window.

### Build-from selected document

If **Build-from selected document** is selected, system does the following:

- System opens the Build-from form, which displays the node list from the source document.
- System makes a transient copy of the source node list: the **transient node list**.
- System makes a list of copies of all clauses from the source document: the **transient clause list**.
- User manages the transient node list using the same context menu options described above for the canonical flow.
- System prevents the user from initiating a build-from if the IsArchived flag on the selected instance is 1 (true). To build from an archived document, the user must first un-archive it.

### Build-to solicitation

If **Build-to solicitation** is selected:

- System verifies that the selected document is a requisition. If it is not, the option is disabled and this path is unreachable (see Context-sensitivity above).
- System presents the user with a list of available solicitation DocTypes and the user selects the target. It is the user's responsibility to select the solicitation DocType in the same tier as the source requisition.
- System initializes the transient node list from the **canonical node list of the selected target DocType**, not from the source document's node list. This ensures the new solicitation starts with the correct structure for its document type.
- System initializes the transient clause list from the canonical clauses of the target DocType.
- System pre-populates fill-in values in the transient clause list with fill-in data carried forward from the source requisition, matched by tag name. It is the administrator's responsibility to keep fill-in names for the same data consistent across document types.
- User manages the transient node list using the same context menu options described above for the canonical flow.
- System prevents the user from initiating a build-to if the IsArchived flag on the selected instance is 1 (true).
- When the user clicks **Generate Document**, the system runs the [Shared Generate Document procedure](#shared-generate-document-procedure). The new Instance row additionally stores a reference to the source requisition instance.

### Build-to award

If **Build-to award** is selected:

- System verifies that the selected document is a solicitation or amended solicitation. If it is not, the option is disabled and this path is unreachable (see Context-sensitivity above).
- System presents the user with a list of available award DocTypes and the user selects the target. It is the user's responsibility to select the award DocType in the same tier as the source solicitation.
- System initializes the transient node list from the **canonical node list of the selected target DocType**.
- System initializes the transient clause list from the canonical clauses of the target DocType.
- System pre-populates fill-in values in the transient clause list with fill-in data carried forward from the source solicitation, matched by tag name. It is the administrator's responsibility to keep fill-in names for the same data consistent across document types.
- User manages the transient node list using the same context menu options described above for the canonical flow.
- System prevents the user from initiating a build-to if the IsArchived flag on the selected instance is 1 (true).
- When the user clicks **Generate Document**, the system runs the [Shared Generate Document procedure](#shared-generate-document-procedure). The new Instance row additionally stores a reference to the source solicitation instance.

### Archive and Un-archive

- If **Archive document** is selected, system sets the IsArchived field in the Instance table to 1, and updates the Dashboard form to reflect this.
- If **Un-archive document** is selected, system sets the IsArchived field in the Instance table to 0, and updates the Dashboard form to reflect this.
