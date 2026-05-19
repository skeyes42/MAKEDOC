# Defining and Illustrating Node Group
## Definition of a Node Group
A node group is a collection of nodes. A node group is defined as follows:
- Node group is named by its association with an inclusion tag.
    - Inclusion tags are defined in the DocType table.
    - Each DocType may have a non-empty InclusionTag field.
    - This field is a JSON string and each element in the string is structured: "tag name: document type", where:
        - Tag name is the text displayed to the user as a selection in the Assembly form
        - Document Type is a DocTypeID in the DocType table.
- So, a Node Group is an adjacency list in the Node table, just like a doc type is an adjacency list in the Node table.