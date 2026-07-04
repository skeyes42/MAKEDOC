    # Testing Report on MAKEDOC for 5.27.2026

## What is tested?
In this test cycle, the context menu options on the Dashboard form where tested. These options include:
- Build from selected document
- View document

## View document results
Here's the scenario:
1. Build a micro solicitation and provide data for the fill-ins during "Generate Document".
2. Observe fill-in data in the assembled document after clicking on the Generate Document button.
3. View this document from the Dashboard.
4. Observe no fill-in data in the document.

## Build from selected document results
Here's the scenario:
1. From Assembly form, build canonical document. Supply fill-in data.
2. Select "build from" option for this document in the Dashboard form.
3. Click on Generate Document.
4. Observe, fill-ins supplied in the original source document are not displayed in the fill-in form.
5. Observe, fill-ins are not moved from the source document to the target document in the Instance table in the database.

## Other observations
1. The {SecNo} plain text fill-in is not being populated in the generated documents. For a given document, the SecNo should increment by 1 throughout the document.
2. The OutputFile field in the Instance table is not being populated.

## Missing features
1. The Instance table has a Title field. Right now, it's not being used. It would be good to have this. This upgrade would involve:
    - Changes to the Dashboard form to support a Title column
    - Changes to the Fill-in form to collect the Title text from the user. (Reference: see create-new-document(amended).md document.)
2. Header nodes can have fill-ins. When generating a document, these fill-ins should show up along with clause fill-ins in the fill-in form.

## Possible directions
Regarding the archiving feature of MAKEDOC:

- With regard to the Archive Document feature:
    - Instead of having a separate form for archiving, we could add "Archive Document" and "Unarchive Document" to the context menu options in the Dashboard form. The IsArchived field and the ArchiveDate fields of the Instance table would be updated accordingly.
    - This would require another change to the Dashboard form to show the user the archive stats of the assembled documents.
    - The rule: you can't build a new document from an existing document if it has been archived. To do this, you'd have to unarchive the source document first.

- With regard to the Template feature in MAKEDOC:
    - Earlier it was suggested each assembled document be assembled starting with the node designated in the DocType table's TemplateBlobID field.
    - In the current system, the assembly process starts with the header node as the "template" for the document.
    - That's perfectly fine. We can talk to this in the book. In terms of the code, this would involve:
        - Updating the DocType table
        - Updating the seed_DocType_table.xlsx seed document
        - Updating the REBUILD_MAKEDOC_DATABASE.ps1 script to remove the step that loads the template nodes
        - Removing the /templates directory