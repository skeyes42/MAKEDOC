# Structural Analysis — MAKEDOC (6.30.2026)

---

## Real Bugs

**1. `MakeDocDb.InsertNode` references `@Sequence` but never binds it** (`MakeDoc.Core/Data/MakeDocDb.cs:142-151`)

The SQL says `VALUES (@NodeID, @NodeType, @Sequence, @Title, @Content)` but the parameter block only adds `@NodeID`, `@NodeType`, `@Title`, `@Content`. `@Sequence` is never bound — runtime error or silent default.

**2. `MakeDocDb.InsertNode` omits the `Type` column, which is `NOT NULL`** (`MakeDocDb.cs:142`)

The schema declares `Type TEXT NOT NULL` with no default. `InsertNode` never writes it. Any call to this method will hit a constraint violation.

**3. `NodeService.Insert` and `NodeService.Update` reference columns that don't exist in the schema** (`NodeService.cs:76-93`)

The SQL references `IsActive`, `CreatedDate`, `ModifiedDate` — none of which are in `InitializeSchema`. The parameter binds for those are commented out, leaving `@active`, `@created`, `@modified` unbound in a live SQL string. Both methods are broken as written. `Deactivate` has the same problem (it sets `IsActive = 0` on a non-existent column).

**4. `Thread.Sleep(1500)` on the UI thread** (`AssemblyForm.cs:1243`)

`OpenInWord` sleeps 1.5 seconds to give Word time to open the file. This blocks the message pump and freezes the entire application for every clause edit.

---

## Architectural Problems

**5. Two parallel, incompatible data-access layers**

`MakDocDb` has its own Node methods (`InsertNode`, `GetNode`, `GetAllNodes`, `UpdateNode`, `DeleteNode`) and `NodeService` has a separate set (`Insert`, `GetById`, `GetAll`, `Update`, `Deactivate`). The two don't agree with each other or the schema. `AssemblyForm` uses `NodeService` for node reads but calls `_db` directly for Instance and LineItem operations — no consistent boundary.

**6. No transaction support — multi-step operations are not atomic**

`MakDocDb` opens and closes a fresh connection per method. There is no way to wrap `InsertInstance` + `AssignLineItemsToInstance` + `InsertUserClause` in a single transaction. If any step fails mid-flow, the database is left in a partial state with no rollback path.

**7. Instance row is persisted before assembly succeeds** (`AssemblyForm.cs:1046`)

`_db.InsertInstance` is called at step 4, but the DOCX is assembled at step 5. If assembly throws, the instance record exists in the database with no corresponding output file and no way to clean it up.

**8. `InclusionTag` record is defined in the UI layer** (`AssemblyForm.cs:1383-1385`)

This is a domain concept that lives at the bottom of `AssemblyForm.cs` as an `internal record`. It should be in `MakeDoc.Core/Models/`.

**9. `Node.IsUserClause` and `Node.IsSpecialClause` are typed `string?`** (`Node.cs:9-10`)

The schema stores them as `INTEGER` (0/1) and `InsertUserClause` writes the literal value `1`. The model declares them as `string?`. The `MapRow` in `NodeService` doesn't even read them back, so they're always `null` in loaded nodes.

---

## Gaps

**10. `MakeDoc.PlumberClient` is an empty stub** — `Class1.cs` contains no code. The analytics form presumably calls the R API but there's no HTTP client implementation.

**11. The test suite has zero real tests** — `UnitTest1.Test1()` is an empty method body. The services (`DocumentAssemblyService`, `FillinService`) have no test coverage despite being the most logic-heavy code in the project.

**12. `MakDocDb` class name has a typo** — File is `MakeDocDb.cs`, class is `MakDocDb` (missing the `e`). Used in every service and form.
