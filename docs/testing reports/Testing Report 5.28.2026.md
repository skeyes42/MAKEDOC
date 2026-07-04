# Testing Report on MAKEDOC for 5.28.2026


# Still to be implemented
- {SecNo} still not populated by system.
- Fill-ins in header node still not working.

# Working
- The view document context menu is working.
- The build from context menu is working.
- The archive and unarchive options are working.


# Build from in the current MAKEDOC system (see "Procurement Flow in create-new-document(amended 5.28.2026)")
In the current (as of 5.27.2060) MAKEDOC system, the build from process is implemented to handle amendments/modifications. That is, build from currently operates such that the result of a build from is a copy of the source document. We need to implement additional options in the context menu of the Dashboard. We keep the current **build from** and add two additional options: **Build to a solicitation** and **built to an award**. 

- Build to a solicitation tells the system several things:
    - The user has selected a previously built requisition from the list of documents in the Dashboard form.
    - The user wants the system to build a new solicitation from the selected requisition in the same tier. That is micro req -> micro sol. Or more precisely in the spirit of build to micro sol <- micro req.

- Similarly, build to an award means:
    - The user has selected a previously built solicitation (or amended solicitation) from the list.
    - The user wants to build an award document from the solicitation in the same tier.  micro award <- micro solicitation.    

As with build from, in build to a solicitation and build to an award, the information (fill-ins) from the source document is moved to the target document.


