# Generate a Canonical Document -- 3 line items

You have access to a Windows 11 environment via computer use tools. Your goal is to
drive my custom MAKEDOC GUI application through a complete document generation cycle.

**Important:** Execute each step fully and confirm it succeeded before moving to the
next.

---

## Phase 1 — Open MAKEDOC

1. Open the application named **MakeDoc.App** using the Start menu or taskbar.
2. Confirm the MAKEDOC Dashboard window is visible on screen. The status bar at the
   bottom should read "MAKEDOC.db connected — ready".

---

## Phase 2 — Add Line Items

3. Left-click the **Tools** menu in the MAKEDOC menu bar.
4. Choose **Line Items...** from the dropdown.
5. In the Line Item Manager dialog, click the **Document Type** dropdown and select
   **micro tier requisition**.
6. If the grid already contains rows from a previous run, that is fine — proceed to
   step 11 (Save All) to confirm the data, then close.
   If the grid is empty ("0 line item(s) loaded"), continue with steps 7–10.
7. Click **Add Row**.
8. Fill in the new row with these values (click each cell to edit it):
   - Description = `Pencils`
   - NAICS = `44566`
   - Unit = `BOX`
   - Qty = `10`
   - Unit Price = `50.0`
9. Click **Add Row** again to add a second row.
10. Fill in the second row:
    - Description = `Batteries`
    - NAICS = `44544`
    - Unit = `EA`
    - Qty = `20`
    - Unit Price = `125.0`
11. Click **Add Row** again to add a third row.   
12. Fill in the third row:
        - Description = `Rulers`
    - NAICS = `88345`
    - Unit = `BOX`
    - Qty = `10`
    - Unit Price = `25.0`
13. Click **Save All**. Confirm the status bar reads "Saved 3 line item(s)."
14. Close the Line Item Manager by clicking its **X** button.

---

## Phase 3 — Build and Generate the Document

15. Left-click the **Tools** menu in the MAKEDOC menu bar.
16. Choose **Assembly...** from the dropdown.
17. Choose **micro tier requisition** from the Document Type list on the left of the form.
18. Wait until the lower-right button is labeled **Generate Document** and the status bar
    reads "Selected: requisition tier requisition".
19. Click the **Generate Document** button **once** and wait 5 seconds. No visible
    response is expected on the first click — this is normal behavior.
20. Click the **Generate Document** button a **second time** and wait up to 5 seconds.
    The **Fill-in Values** modal dialog should now appear.
21. In the Fill-in Values dialog, supply only the following two values
    (leave all other fields blank):
    - **Document Title** = `Buy pencils, batteries and rulers`
    - **Requisition Number** = `123456789`
22. Click the **Generate Document** button inside the Fill-in Values dialog
    (lower-right of that modal).
23. A Windows **Save As** dialog will open. The default filename (system-generated,
    e.g. `micro tier requisition_<uuid>_<datetime>.docx`) and default folder
    (`"C:/Users/skeye/BOOK2/MAKEDOC/docs/generated_docs/assembled_documents"`) are correct — do not change them.
24. Click **Save**.
25. Confirm the MAKEDOC Dashboard (visible behind or after the Assembly form) shows a
    new requisition tier requisition entry dated today with the title
    "Pencils and Batteries Proc...".
26. Close the Document Assembly form by clicking its **X** button.

---

## Phase 4 — Close MAKEDOC

27. Confirm the MAKEDOC Dashboard is now the active window and the document list
    shows the new Micro tier requisition entry dated today.
28. Close the MAKEDOC Dashboard by clicking its **X** button.

---

## Error Handling Notes

**Generate Document — first click has no visible effect (step 17):**
This is confirmed normal behavior. The button registers the first click silently.
The Fill-in Values dialog only appears on the second click (step 18). Do not skip
the wait between clicks.

**NAICS and Unit cells appear blank after Tab-entry (Phase 2):**
These cells may display empty until you double-click on them to commit the value.
The data is present — double-clicking reveals it. Click Save All regardless; the
values will be saved correctly.

**Extended amounts show 0.00 before Save All:**
Normal — extended amounts recalculate on save.

**Line items already present from a previous run (step 6):**
This is fine. Proceed to Save All to confirm totals, then close.

**Build From Document form does NOT open after Save (step 22):**
The Assembly route saves the document directly. No second-pass Build From Document
form opens — this is the correct behavior for Tools > Assembly. The document is
fully saved once the Save As dialog is dismissed.

**Save As dialog appears behind MAKEDOC window:**
If the Save dialog is not visible, use open_application to bring MakeDoc.App
forward, then click the Save button at the bottom of the screen.
