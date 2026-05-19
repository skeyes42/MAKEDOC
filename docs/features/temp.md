# Create new document from canonical document and from build-from document

## Definition of a Node Group
A **node group** is a collection of nodes. A node group is defined as follows:
- Node group is named by its association with an inclusion tag.
    - Inclusion tags are defined in the **DocType** table.
    - Each DocType may have a non-empty InclusionTag field.
    - This field is a JSON string and each element in the string is structured: "tag name: doctype", where:
        - Tag name is the text displayed to the user as a selection in the Assembly form
        - Doctype is a **DocTypeID** in the DocType table.
- So, a Node Group is an adjacency list in the Node table, just like a doc type is an adjacency list in the Node table.
- A node group can be assembled like a document.
- A node group,like a document types, has an associated template and a header node.

## Creating a document starting with canonical node list
- User selects Doc Type in the Assembly form and receives a display of canonical nodes (from the NodeHierarchy table).
- System makes a mutable copy of the node list: the **transient node list**
- System makes a mutable list of copies of all canonical clauses: the **transient clause list**
- User manages transient node list by picking options from the context menu:
    - **Insert User Clause** — System opens a file‑open dialog; the selected file is inserted above the current clause. A user node is added to the transient node list, and the DOCX 
    of the new clause is added to the transient clause list.
    - **Delete Clause** — System deletes the selected clause. Transient node list is updated.
    - **Edit Clause** — System opens a Word session in a dialog; user edits the clause. The edited clause is saved in the transient clause list.
    - **Move Clause** — System moves the clause under cursor control. The transient node list is updated to reflect the move.
    - **Browse Clause** — System opens a read‑only Word session displaying the selected clause. Neither the transient node list nor the transient clause list are updated.
	- **Reset Clause List** — System restores the canonical list from the NodeHierarchy. The transient node list and transient clause list are restored to their original states.
    - **Insert Special Clauses** - System pops-up a list of possible inclusion tags for the doc type. Each inclusion tag corresponds to an adjacency list in the Node table.
    This group of nodes is called a **node group**. (See "Node Groups.md"). If the user selects an Inclusion tag, then all of the nodes in the node group are at the cursor location. These node are not added to the transient clause list since no edits to these nodes are allowed.
- User closes Assembly for without clicking on **Generate Document** button. System prompts with "Click on Generate Button to keep changes."
- User clicks on **Generate Document** button:
    - System collects all of the fill-ins found in clauses in the transient clause list.
    - System puts up a modal Fill-in form to collect data for the fill-ins found.
    - User provides fill-in data into the Fill-in form.
    - User closes the Fill-in form.
    - System creates new row in Instance table from the **transient node list** and the **transient clause list**
    - The Node table is updated. A new user clause (UC-\<short guid\>) is added for every clause in the transient clause list that have been edited.
    - System replaces the fill-in tags with the fill-in data that was collected in the Fill-in form.
    - System assembles document.
    - System update Instance table with new Instance row.
    - System opens **file save dialog**
    - User saves newly assembled document.

<div style="page-break-before: always;"></div>

## Creating a document via build-from
- User selects an existing document (instance) from the list in the Dashboard form.
- User manages new document from the context menu:
    - **Build-from selected document**
    - **View document**
- If the view document option is selected, system re-assembles source document and presents in a read-only DOCX editor window.
- If build-from option is selected, systems does the following:
    - System opens Build-from form. This form displays the node list from the source document.
    - System make transient copy of the source node list: **transient node list**
    - System makes a list of copies of all clauses from the source document: **transient clause list**. 
    - User manages **transient node list** (same context menu options as for new document made from canonical clauses for doc type).

If user closes Build-from form, he/she get a prompt: "Click on Generate Document to save, otherwise changes are lost."

- If user clicks on **Generate Document**:
    - System collects all of the fill-ins found in the transient clause list.
    - System puts up a modal Fill-in form to collect data for the fill-ins found.
    - User closes Fill-in form.
    - System creates new row in Instance table.
    - The Node table is updated. A new user clause (UC-\<short guid\>) is added for every clause in the transient clause list that have been edited.
	- System populates new Instance row with **transient node list**, **transient clause list**, and other information from the source document.
    - System replaces the fill-in tags with the fill-in data that was collected in the Fill-in form.
    - System assembles the build-from document
    - System updates Instance table with new Instance row.
    - System opens **file save dialog**
    - User saves newly assembled document.



