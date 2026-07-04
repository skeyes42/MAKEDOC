# MAKEDOC Clause Classification Report

## Summary

| Type | Description | Count |
|------|-------------|-------|
| **A** | Pure boilerplate — no reference to items, quantities, or dollar amounts | 43 |
| **B** | Aggregate — references total dollar amount, tier, or procurement method | 9 |
| **C** | Line-item specific — references item descriptions, quantities, unit prices | 36 |
| | **Total** | **88** |

---

## TYPE C — LINE-ITEM SPECIFIC

*These clauses contain hard-coded item references (pencils, batteries, quantities, unit prices). Each needs template tokens to replace the specific content.*

### Micro Tier

| Clause | Phase | Section Title | Key Tokens Needed |
|--------|-------|---------------|-------------------|
| NL-0003 | Requisition | SECTION {SecNo} — PURPOSE | quantity, batteries |
| NL-0004 | Requisition | SECTION {SecNo} — DESCRIPTION OF ITEMS | 000 units, batteries, 20 units |
| NL-0016 | Solicitation | SECTION {SecNo} — ITEMS REQUESTED | 000 units, batteries, 20 units |
| NL-0018 | Solicitation | SECTION {SecNo} — QUOTE SUBMISSION | batteries, unit price |
| NL-0025 | Award | State of Northlandia | batteries |
| NL-0027 | Award | SECTION {SecNo} — ITEMS ORDERED | 000 units, batteries, unit price, 20 units |

### Standard Tier

| Clause | Phase | Section Title | Key Tokens Needed |
|--------|-------|---------------|-------------------|
| NL-0038 | Requisition | State of Northlandia | batteries |
| NL-0065 | Solicitation | STATE OF NORTHLANDIA | batteries |
| NL-0066 | Solicitation | SECTION {SecNo} — INTRODUCTION | batteries |
| NL-0070 | Solicitation | SECTION {SecNo} — SUBMISSION INSTRUCTIONS | batteries |
| NL-0071 | Solicitation | SECTION {SecNo} — DESCRIPTION OF GOODS | 200 units, batteries, 500 units, quantity |
| NL-0074 | Solicitation | SECTION {SecNo} — PRICING | extended price, unit price |
| NL-0095 | Award | SECTION {SecNo} — CONTRACT FORMATION | batteries |

### Complex Tier

| Clause | Phase | Section Title | Key Tokens Needed |
|--------|-------|---------------|-------------------|
| NL-0116 | Requisition | STATE OF NORTHLANDIA | batteries |
| NL-0120 | Requisition | SECTION {SecNo} — REQUISITION PURPOSE | batteries |
| NL-0121 | Requisition | SECTION {SecNo} — JUSTIFICATION / NEED STATEMENT | batteries |
| NL-0122 | Requisition | SECTION {SecNo} — DESCRIPTION OF REQUIRED ITEMS | batteries |
| NL-0123 | Requisition | SECTION {SecNo} — ESTIMATED QUANTITIES | 400 units, 250 units, batteries, 300 units |
| NL-0126 | Requisition | SECTION {SecNo} — DELIVERY REQUIREMENTS | batteries, unit price |
| NL-0127 | Requisition | SECTION {SecNo} — SPECIAL REQUIREMENTS | battery |
| NL-0139 | Solicitation | State of Northlandia | batteries |
| NL-0140 | Solicitation | SECTION {SecNo} — PURPOSE | batteries |
| NL-0141 | Solicitation | SECTION {SecNo} — BACKGROUND | batteries |
| NL-0142 | Solicitation | SECTION {SecNo} — SCOPE OF WORK | batteries, quantity, battery, clin, 000 units |
| NL-0146 | Solicitation | SECTION {SecNo} — MANDATORY VENDOR QUALIFICATIONS | battery, batteries |
| NL-0147 | Solicitation | SECTION {SecNo} — PRICING STRUCTURE | batteries, battery, clin, pencil, qty |
| NL-0148 | Solicitation | SECTION {SecNo} — PRICING STRUCTURE | battery, pencil |
| NL-0150 | Solicitation | SECTION {SecNo} — ATTACHMENTS | battery |
| NL-0163 | Award | State of NorthLandia | batteries |
| NL-0168 | Award | SECTION {SecNo} — ATTACHMENTS | battery |
| NL-0170 | Award | SECTION {SecNo} — PURPOSE | batteries |
| NL-0172 | Award | SECTION {SecNo} — CONTRACT LINE ITEMS (CLINs) | batteries, 144 box, battery, clin, 144   box, each   $ |
| NL-0173 | Award | SECTION {SecNo} — SPECIFICATIONS | batteries |
| NL-0174 | Award | SECTION {SecNo} — ORDERING AND DELIVERY | batteries, clin, quantity, unit price |
| NL-0175 | Award | SECTION {SecNo} — BATTERY RECYCLING PROGRAM | battery, batteries |
| NL-0176 | Award | SECTION {SecNo} — CONTRACTOR QUALIFICATIONS | battery |

## TYPE B — AGGREGATE DOLLAR / TIER REFERENCES

*These clauses reference totals or procurement tier but not specific items. They can be updated via FillinData tokens (e.g. `{{total_amount}}`, `{{tier_label}}`).*

### Micro Tier

| Clause | Phase | Section Title | Key Tokens Needed |
|--------|-------|---------------|-------------------|
| NL-0005 | Requisition | SECTION {SecNo} — ESTIMATED COST | total cost |
| NL-0006 | Requisition | SECTION {SecNo} — PROCUREMENT METHOD | micro-purchase |
| NL-0015 | Solicitation | SECTION {SecNo} — RFQ AND REQUISITION INFORMATION | informal, total cost |

### Standard Tier

| Clause | Phase | Section Title | Key Tokens Needed |
|--------|-------|---------------|-------------------|
| NL-0043 | Requisition | SECTION {SECNO} — CONTRACTUAL TERMS AND CONDITIONS | tier |
| NL-0047 | Requisition | SECTION {SECNO} — BUDGET AND FUNDING | total cost |
| NL-0076 | Solicitation | SECTION {SecNo} — TERMS & CONDITIONS (STANDARD TIER) | tier |
| NL-0094 | Award | STANDARD-TIER AWARD DOCUMENT | tier |
| NL-0097 | Award | SECTION {SecNo} — CONTRACT ITEMS AND PRICING | total contract |

### Complex Tier

| Clause | Phase | Section Title | Key Tokens Needed |
|--------|-------|---------------|-------------------|
| NL-0125 | Requisition | SECTION {SecNo} — PROCUREMENT METHOD REQUESTED | competitive sealed |

## TYPE A — PURE BOILERPLATE

*No changes needed. These clauses are fully reusable across any procurement.*

### Micro Tier

| Clause | Phase | Section Title | Key Tokens Needed |
|--------|-------|---------------|-------------------|
| NL-0001 | Requisition | State of Northlandia | — |
| NL-0002 | Requisition | SECTION {SecNo} — REQUESTING AGENCY INFORMATION | — |
| NL-0007 | Requisition | SECTION {SecNo} — APPROVALS | — |
| NL-0014 | Solicitation | State of Northlandia | — |
| NL-0017 | Solicitation | SECTION {SecNo} — DELIVERY REQUIREMENTS | — |
| NL-0019 | Solicitation | SECTION {SecNo} — BASIS OF AWARD | — |
| NL-0026 | Award | SECTION {SecNo} — PURCHASE ORDER AND SOLICITATION INFORMATIO | — |
| NL-0028 | Award | SECTION {SecNo} — DELIVERY | — |
| NL-0029 | Award | SECTION {SecNo} — PAYMENT | — |
| NL-0030 | Award | SECTION {SecNo} — ACCEPTANCE | — |
| NL-0031 | Award | SECTION {SecNo} — SIGNATURES | — |

### Standard Tier

| Clause | Phase | Section Title | Key Tokens Needed |
|--------|-------|---------------|-------------------|
| NL-0039 | Requisition | SECTION {SECNO} — TECHNICAL SPECIFICATIONS | — |
| NL-0040 | Requisition | SECTION {SECNO} — DELIVERY, MILESTONES, AND TIMELINES | — |
| NL-0041 | Requisition | SECTION {SECNO} — REQUISITION OVERVIEW | — |
| NL-0042 | Requisition | SECTION {SECNO} — VENDOR QUALIFICATIONS | — |
| NL-0044 | Requisition | SECTION {SECNO} — ATTACHMENTS AND SUPPORTING DOCUMENTS | — |
| NL-0045 | Requisition | SECTION {SECNO} — APPROVALS | — |
| NL-0046 | Requisition | SECTION {SECNO} — REQUISITION OVERVIEW | — |
| NL-0048 | Requisition | SECTION {SECNO} — PROCUREMENT METHOD AND JUSTIFICATION | — |
| NL-0049 | Requisition | SECTION {SECNO} — RISK ASSESSMENT | — |
| NL-0050 | Requisition | SECTION {SECNO} — COMPLIANCE AND REGULATORY REQUIREMENTS | — |
| NL-0051 | Requisition | SECTION {SECNO} — EVALUATION CRITERIA | — |
| NL-0073 | Solicitation | SECTION {SecNo} — DELIVERY REQUIREMENTS | — |
| NL-0075 | Solicitation | SECTION {SecNo} — EVALUATION CRITERIA | — |
| NL-0096 | Award | SECTION {SecNo} — ADDITIONAL PROVISIONS | — |
| NL-0098 | Award | SECTION {SecNo} — DELIVERY REQUIREMENTS | — |
| NL-0099 | Award | SECTION {SecNo} — SUPPLIER OBLIGATIONS | — |
| NL-0102 | Award | SECTION {SecNo} — PAYMENT | — |
| NL-0103 | Award | SECTION {SecNo} — LIABILITY | — |
| NL-0104 | Award | SECTION {SecNo} — TERMINATION | — |

### Complex Tier

| Clause | Phase | Section Title | Key Tokens Needed |
|--------|-------|---------------|-------------------|
| NL-0117 | Requisition | SECTION {SecNo} — REQUESTING AGENCY INFORMATION | — |
| NL-0118 | Requisition | SECTION {SecNo}  — ATTACHMENTS | — |
| NL-0119 | Requisition | SECTION {SecNo} — APPROVALS | — |
| NL-0124 | Requisition | SECTION {SecNo} — FUNDING INFORMATION | — |
| NL-0145 | Solicitation | SECTION {SecNo} — CONTRACT TYPE | — |
| NL-0149 | Solicitation | SECTION {SecNo} — EVALUATION CRITERIA | — |
| NL-0164 | Award | SECTION {SecNo} — PARTIES | — |
| NL-0165 | Award | SECTION {SecNo} — CONTRACT ADMINISTRATION | — |
| NL-0166 | Award | SECTION {SecNo} — MODIFICATIONS | — |
| NL-0167 | Award | SECTION {SecNo} — TERMINATION | — |
| NL-0169 | Award | SECTION {SecNo} — SIGNATURES | — |
| NL-0171 | Award | SECTION {SecNo} — TERM OF CONTRACT | — |
| NL-0177 | Award | SECTION {SecNo} — PERFORMANCE STANDARDS | — |
