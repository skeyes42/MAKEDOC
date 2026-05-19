# MAKEDOC Fill-in Variables

## Context
The MAKEDOC system support a fill-in feature (See features.md) The documents in the original, delivered database can 
be categorized into three high-level **tiers** based on the amount of the procurement:

1. Micro tier -- for small dollar procurements
2. Standard tier -- for procurements between the Micro level and the Complex level
3. Complex tier -- for large dollar procurements

In addition, each tier is broken down into the three phases of a procurment (independent of the dollar amount):

1. Requisition Phase -- requirements/requisition Phase
2. Solicitation Phase
3. Award Phase

As a result there are nine high-level categories. Within each category, there are a group of nodes (type clause, section, subsection)
Each type of node can have fill-ins. The fill-in feature is implemented using SDT fields in Word. Each SDT is configured so that
its **title** is the hover text above the field, and the **tag** specification is the name of the field.

The purpose of this document is to log the field information (title and tag) for documents falling into the nine categories.

## Fill-in Variables for Documents

### Micro requisition fill-in fields (tag, title and description)
- RequisitionNumber, Requisition Number, Requisition number
- DASContact, DAS Contact, DAS official responsible for submitting the Requisition
- DASEmail, DAS Email, DAS email address
- DASPhone, DAS Phone, DAS phone number
- TotalCost, Total Cost, Estimated total cost of the procurement
- RequestingOfficial, Requesting Official, Requesting official for the Requisition
- BudgetApproval, Budget Approval, Budget approval official
- ProcurementApproval, Procurement Approval, Procurement approval official
- RequestingDate, Requesting Date, Date of signature
- BudgetDate, Budget Date, Date of signature
- ProcurementDate, Procurement Date, Date of signature

### Micro solicitation fill-in fields
=== NL-0014.docx ===
SDT Found  Tag: SolicitationNumber

=== NL-0015.docx ===
SDT Found  Tag: RFQissueDate
SDT Found  Tag: RFQresponseDueDate

### Micro award fill-in fields

### Standard requisition fill-in fields

### Standard solicitation fill-in fields

### Standard award fill-in fields

### Complex requisition fill-in fields

### Complex solicitation fill-in fields

### Complex award fill-in fields