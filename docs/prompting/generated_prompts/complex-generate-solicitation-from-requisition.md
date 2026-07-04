# Generate a Solicitation from a Requisition

You have access to a Windows 11 environment via computer use tools. Your goal is to
drive my custom MAKEDOC GUI application through a complete solicitation generation
cycle, building from an existing micro-tier requisition.

**Important:** Execute each step fully and confirm it succeeded before moving to the
next.

**Critical — Cowork task widget:** The Cowork task list widget occupies the right side
of the screen. Before clicking any button in a Document Assembly window, drag that
window to the left so the Generate Document button is in the center of the screen,
clear of the widget. Clicks intercepted by the widget are silent — the app receives
nothing.

---

## Phase 1 — Open MAKEDOC

1. Open the application named **MakeDoc.App** using the Start menu or taskbar.
2. Confirm the MAKEDOC Dashboard window is visible on screen. The status bar at the
   bottom should read "MAKEDOC.db connected — ready".

---

## Phase 2 — Select Requisition and Build to Solicitation

3. In the MAKEDOC Dashboard document list, locate the most recent **complex tier requisition** 
4. Right-click that row.
5. Choose **Build to solicitation...** from the context menu.
6. In the **Select Document Type** dialog, choose **complex tier solicitation**.
7. Confirm the **MAKEDOC — Build To: complex tier solicitation** Document Assembly window opens.

---

## Phase 3 — Generate the Solicitation

8. Drag the Document Assembly window to the **left side of the screen** so the
   Generate Document button (lower-right of the form) is in the center of the
   screen — away from the Cowork task widget on the right.
9. Call `open_application("MakeDoc.App")` to ensure MAKEDOC is the foreground app.
10. Click the **Generate Document** button **once**. The **Fill-in Values** modal
    dialog should appear immediately (one click is sufficient in the Build To route).
11. In the Fill-in Values dialog, supply the following values
    (leave all other fields as-is or blank):
    - **Solicitation Number** = `{{SolNumber}}`
    - **Title** = `{{Title}}`
    - **Document Title** is pre-filled from the source requisition — do not change it.
12. Click the **Generate Document** button inside the Fill-in Values dialog.
13. The **MAKEDOC — Review Line Items** (Line Item Manager) dialog opens. It shows
    line items copied from the source requisition. Review them, then close the dialog
    by clicking its **X** button to accept the items.
14. The Windows **Save As** dialog opens. The default filename
    (e.g. `complex tier solicitation_<uuid>_<datetime>.docx`) and default folder
    (`C:/Users/skeye/BOOK2/MAKEDOC/docs/generated_docs/assembled_documents`) are correct — do not change them.
15. Click **Save**.
16. Confirm the Document Assembly status bar reads
    "Saved:  tier solicitation_<filename>."

---

## Phase 4 — Close MAKEDOC

17. Close the Document Assembly form by clicking its **X** button.
18. Confirm the MAKEDOC Dashboard is now the active window. The new Micro tier
    solicitation entry should appear in the list dated today.
19. Close the MAKEDOC Dashboard using **File > Exit** from the menu bar.

---

## Error Handling Notes

**Generate Document button has no visible effect (step 10):**
Almost certainly the Cowork task widget is intercepting the click. Drag the Document
Assembly window further left so the button clears the widget area, call
`open_application("MakeDoc.App")` to restore focus, then click again.

**Build To route requires only ONE click (unlike Tools > Assembly):**
In this route, a single click on Generate Document opens the Fill-in Values dialog
directly. Do not click twice — a second click generates a second document silently.

**Fill-in Values dialog appears behind another window:**
Call `open_application("MakeDoc.App")` to bring MAKEDOC forward. The dialog should
surface.

**Line Item Manager grid appears empty:**
The grid shows headers but may not display row data visibly. The line items are
present in the database and will be written to the document. Close with X to proceed.

**Save As dialog appears behind MAKEDOC window:**
Call `open_application("MakeDoc.App")` to bring MAKEDOC forward, then click Save.

**Dashboard entry for the new solicitation is missing after save:**
The document file is saved correctly regardless. The missing entry may indicate the
row was accidentally archived (clicking near the dashboard's title bar area can
interact with the grid). The file in `C:\temp\assembled_documents\` is the
authoritative record.

**Dashboard X button does not close the window:**
Use **File > Exit** from the menu bar instead.
