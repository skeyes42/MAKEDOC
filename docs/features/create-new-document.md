# Create new document from canonical document and from build-from document

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
2. System opens a modal **Fill-in form** to collect data for the fill-ins.
3. User provides fill-in data and closes the Fill-in form.
4. For every clause in the transient clause list that has been edited, system writes a new user clause (UC-\<short guid\>) to the Node table. A new UC- row is always created, even when the source clause was itself a UC- clause — UC- rows are never updated in place.
5. System creates a new row in the **Instance table** from the transient node list and transient clause list (now referencing the newly written UC- node IDs).
6. System replaces the fill-in tags in the transient clauses with the data collected from the Fill-in form.
7. System assembles the document.
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
    - **View document**
- If **View document** is selected, system re-assembles the source document and presents it in a read-only DOCX editor window.
- If **Build-from selected document** is selected, system does the following:
    - System opens the Build-from form, which displays the node list from the source document.
    - System makes a transient copy of the source node list: the **transient node list**.
    - System makes a list of copies of all clauses from the source document: the **transient clause list**.
    - User manages the transient node list using the same context menu options described above for the canonical flow.
- When the user clicks **Generate Document**, the system runs the [Shared Generate Document procedure](#shared-generate-document-procedure). The new Instance row additionally inherits applicable metadata (e.g., source-document reference) from the source document.