# Lab 4 – Verification Evidence (Appendix C, A01–A19)

This file records each Appendix C scenario with its **setup, action, expected result, observed result and evidence**.

## How the evidence was collected

| Kind | What it proves | Where |
|---|---|---|
| **Automated tests** (66 xUnit tests) | Every rule, rejection and failure path, checked through the real services against PostgreSQL. Each test starts from a reset, reseeded disposable database `equipmentborrowing_test`. Every check after an operation uses a **new `DbContext`** (`FreshContext()`). | [`terminal-output/automated-tests.txt`](evidence/terminal-output/automated-tests.txt): *Total tests: 66, Passed: 66* |
| **Clean-database run** | The migrations build the schema on an empty database. PostgreSQL itself refuses invalid writes, and an in-transaction failure leaves nothing behind. | [`terminal-output/clean-database-migration-and-constraints.txt`](evidence/terminal-output/clean-database-migration-and-constraints.txt) (disposable `equipmentborrowing_clean`, dropped afterwards) |
| **UI screenshots** | What staff see: success (green), rejection (orange), error (red with Retry), empty states (italic). | [`evidence/screenshots/`](evidence/screenshots/) |
| **Restart readback** | Data survives closing the app. After each app session the app was closed and `psql` read the tables over a **new connection**. The next session was a new app process. | [`terminal-output/restart-1…3`](evidence/terminal-output/) |
| **Offline / empty runs** | Error and empty states. The app was started with an unreachable database (port 1), and separately on an empty disposable database. | `a18-offline-*`, `a01-empty-catalog`, … and [`empty-database-setup.txt`](evidence/terminal-output/empty-database-setup.txt) |

Environment: Windows 11, PostgreSQL 18.6, .NET 10, EF Core 10.0.12, Npgsql EF Core provider 10.0.3, Avalonia 12.1.1. Tests and screenshots are from 2026-11-11, so "today" in the dates is 2026-11-11.

**Seed data.** It is inserted only when the database has no students and no equipment. It has 4 students: Ana and Ben have active and returned history, Carla is restricted, and Dan has no history. It has 6 items: 2 borrowed, 4 available, and the microphone has never been borrowed. There are 5 borrowings: 2 active and 3 returned. During the lab, Eve Garcia (`2023-005`) and Speaker 01 were added through the app.

Screenshot names below are files in `evidence/screenshots/`.

---

## A01 / UC-01 – View Equipment Catalog
- **Setup:** Real database, plus a disposable `equipmentborrowing_empty` database (schema plus one student, no equipment), plus a run with an unreachable database.
- **Action:** Open the catalog in each situation.
- **Expected:** The list matches the database (code, name, description, availability). Empty and error states look different. Nothing changes.
- **Observed:** The catalog rows match the `Equipment` table readback, for example Speaker 01 and Tripod 01 Available and the rest Borrowed in `restart-1`. The empty database shows the italic *"The equipment catalog is empty."* The unreachable database shows a red *"Could not load data: the PostgreSQL database cannot be reached… click Retry"*, plus a startup banner. Retry keeps the error while the database is down.
- **Evidence:** `a01-catalog-on-postgresql`, `seq9-restart-catalog` vs. `terminal-output/restart-3` Equipment table; `a01-empty-catalog`; `a18-offline-catalog-error`, `a18-offline-catalog-after-retry`; test `LoadingAList_WhenDatabaseIsDown_ReturnsAnErrorResultNotAnException`.

## A02 / UC-02 – Find Available Equipment
- **Setup / Action:** Tick "Show available equipment only" on the real database, on the empty database, and with the database down.
- **Expected:** Only `IsAvailable = true` items. A "no available items" message that is different from the error.
- **Observed:** Only available items are listed. The empty database shows *"No equipment is available right now."* The offline state shows the red load error with Retry, which uses the same load path.
- **Evidence:** `a02-no-available-equipment`, `a18-offline-catalog-after-retry`; compare with the `IsAvailable` column in the readbacks.

## A03 / UC-03 – Borrow Equipment
- **Setup:** New student `2023-0100 Lab Four Tester` (eligible, 0 active) and new item `EQ-CAM-02` (available).
- **Action:** Borrow Camera 02 for the tester (expected return 2026-11-18). Separately: restricted student, missing student/item, unavailable item, 4th item, and expected date today/past/missing.
- **Expected:** One `Active` row and the item `IsAvailable = f`, and both survive a restart. Every rejection leaves the data unchanged.
- **Observed:** Green *"Camera 02 Sony (EQ-CAM-02) issued to Lab Four Tester. Expected return: 2026-11-18."* `restart-1` shows borrowing **#11** (2023-0100 / EQ-CAM-02 / Active) and Camera 02 `f`. After the restart, Active Borrowings still lists #11. Borrowing while restricted gave orange *"Lab Four Tester is restricted and cannot borrow equipment. Nothing was saved."*, and no new row appears in `restart-2`. The other rejections are covered by tests that compare row counts and availability before and after.
- **Evidence:** `seq3-borrow-new-item-to-new-student`, `seq3-active-after-restart`, `seq4-restricted-borrow-rejected`, `restart-1/2`; tests `Borrow_ValidRequest_SavesActiveBorrowingAndMarksItemUnavailable`, `Borrow_RestrictedStudent_IsRejectedAndSavesNothing`, `Borrow_UnavailableItem_IsRejected`, `Borrow_MissingStudentOrItem_ReturnsNotFound`, `Borrow_FourthActiveItem_IsRejected`, `Borrow_ExpectedDateTodayPastOrMissing_IsRejected`. The database also blocks a second active row (`IX_Borrowings_OneActivePerEquipment`) and a non-later date (`CK_Borrowings_ExpectedAfterCheckout`), see A17.

## A04 / UC-04 – View Active Borrowings
- **Action:** Open Active Borrowings on the real database, the empty database, and offline.
- **Expected:** Only `Active` rows, each with its student and equipment. Empty and error states are different.
- **Observed:** The list (#2, 4, 7, 8, 9, 10, 11) matches the `Active` rows of `restart-1`. Returned rows #1, 3, 5 are not shown. The empty database shows *"There are no active borrowings."* Offline shows the red error with Retry.
- **Evidence:** `seq3-active-after-restart` vs. `restart-1`; `a04-no-active-borrowings`; `a18-offline-active-borrowings-error`; test `Search_StatusFilters_ReturnActiveReturnedOrAll` (Active = 2 of 5 seed rows).

## A05 / UC-05 – Return Equipment
- **Setup:** Borrowing #11 is active and its student is restricted.
- **Action:** Return #11. Then try to return already-returned #1 again, and return a missing borrowing (test).
- **Expected:** The same row becomes `Returned` with today's date and the item becomes available. History is kept. The repeat return changes nothing. All of this survives a restart.
- **Observed:** Green *"Camera 02 Sony (EQ-CAM-02) was returned on 2026-11-11 and is available again."* `restart-2` and `restart-3` show **#11 Returned 2026-11-11**, still linked to 2023-0100 / EQ-CAM-02. The repeat return gave orange *"Borrowing #1 was already returned on 2026-10-27. Nothing was changed."* A missing id gives NotFound in the test.
- **Evidence:** `seq4-return-while-restricted`, `a05-repeat-return-rejected`, `restart-2/3`; tests `Return_ActiveBorrowing_MarksReturnedAndMakesItemAvailable`, `Return_AlreadyReturned_IsRejectedAndChangesNothing`, `Return_MissingBorrowing_ReturnsNotFound`.

## A06 / UC-06 – Check Out Multiple Equipment Items
- **Setup:** Disposable test database for every rejection and both failure tests. The UI demonstration ran on the local development database.
- **Action and observed (UI):**
  - Tester + Camera 02 + Speaker 01 succeeded: *"… issued to Lab Four Tester."* `restart-2` shows **#12 and #13 Active**, both items `f`, and they still exist after the next restart (`restart-3`).
  - Eve + Camera 02 (borrowed) + Tripod 01 was rejected: *"Camera 02 Sony (EQ-CAM-02) is not available. Nothing was saved."* `restart-2` has no row for Eve, and Tripod 01 is still `t`.
  - Earlier UI runs: duplicate item, fewer than two, over the limit (Ana 1 + 3) and restricted Carla were each rejected with "Nothing was saved". **Exactly at the limit** (Ana 1 + 2 = 3) succeeded.
- **Safe in-transaction failure:** the test seam *"Simulate a failure AFTER the first item is written"* makes `IssueAsync` call `SaveChangesAsync` for the first borrowing (an `INSERT` sent to PostgreSQL inside the open transaction) and then throw. The app shows red *"…The whole transaction was rolled back, so NOTHING was saved."*
  - **Proof the write happened:** test `CheckOut_FailureAfterFirstWrite_RollsBackEverything` reads the `Borrowings_Id_seq` sequence before and after. It went up, so an `INSERT` reached PostgreSQL. A fresh read then shows 0 new rows and both items still available.
  - The same thing is visible in the real database: borrowing **#6 is missing**. That number was used by the rolled-back UI demonstration (`a06-failure-after-first-write-rolled-back`).
  - The clean-database run shows it in SQL too (Test 9): the first `INSERT` and `UPDATE` succeed inside the transaction, the second write fails, `COMMIT` returns `ROLLBACK`, and a fresh connection shows only the setup rows.
- **Unknown outcome (BR-18):** the seam *"Simulate a lost connection right after COMMIT"* gives red *"…the result is UNKNOWN. The changes were either fully saved or not saved at all. Refresh…"* There is no automatic retry and no "nothing saved" claim. Checking the state afterwards showed both rows (#9, #10), saved all together. Test `CheckOut_LostCommitAcknowledgement_ReportsUnknownOutcome` checks the same thing.
- **Evidence:** `seq5-multi-item-checkout-success`, `seq6-checkout-with-unavailable-item-rejected`, `a06-*` (10 screenshots), `restart-2/3`, `clean-database-migration-and-constraints.txt` Test 9; all 12 tests in `MultiItemCheckoutTests`.

## A07 / UC-07 – View Student Borrowing History
- **Action:** Select Ana (active and returned), Lab Four Tester (active and returned), and Eve (no history). Missing student and failed load are covered by tests.
- **Expected:** Only that student's rows, with correct item, dates and status. Empty, not-found and error results are all different.
- **Observed:** Ana shows only her rows. Tester shows #13 Speaker Active, #12 Camera 02 Active, and #11 Camera 02 Returned 2026-11-11, which matches `restart-3`. Eve shows *"Eve Garcia has no borrowing history yet."* "Loading history…" appears while the history loads. A stale student gives the not-found state, and a database failure gives the error state.
- **Evidence:** `a07-history-ana-active-and-returned`, `seq5-permit-and-student-history`, `seq9-restart-student-history`, `a07-history-empty-student`; tests `StudentHistory_ShowsOnlyThatStudentsActiveAndReturnedRecords`, `StudentHistory_StudentWithNoBorrowings_IsEmptyNotAnError`, `StudentHistory_MissingStudent_IsNotFound`. Code: `StudentsViewModel.LoadHistoryAsync` awaits the service and ignores answers for a student that is no longer selected (`_historyRequestNumber`).

## A08 / UC-08 – View Equipment Borrowing History
- **Action:** Select Laptop 01 (two different students), Camera 02, and Speaker 01 before it was ever borrowed.
- **Observed:** Laptop 01 shows Ana (Returned) and Ben (Active). Camera 02 shows #12 Active and #11 Returned 2026-11-11, both for 2023-0100. That is **the same returned event and dates as the student history** (integrated sequence step 7). A never-borrowed item shows *"Speaker 01 has never been borrowed."*
- **Evidence:** `a08-history-laptop01-two-students`, `seq7-equipment-history-same-event`, `a08-history-never-borrowed`; tests `EquipmentHistory_*` (3).

## A09 / UC-09 – Search, Filter, and Sort Borrowings
- **Action:** Search `aNa` (case and partial), `laptop` + Returned (combined), sort by expected return date descending, `tester` + Returned, `zzz`, Clear, and offline.
- **Observed:** `aNa` finds only Ana's rows. `laptop` + Returned gives exactly 1 row. With the descending sort, the rows due 2026-11-18 come first, in order #7, 8, 9, 10, because borrowing # breaks ties. `tester` + Returned gives #11 only. `zzz` shows *"No borrowings match these criteria."* (an empty state, not an error). Offline shows the red error with Retry.
- **Evidence:** `a09-search-case-insensitive-partial`, `a09-combined-text-and-returned`, `a09-sort-expected-return-descending`, `seq8-search-tester-returned`, `a09-no-matches`, `a09-empty-database-no-matches`, `a18-offline-search-error`; the 9 `Search_*` tests run against known seed data and cover each field, each status, both sorts in both directions, the combination, the tie-breaker and no matches.

## A12 / UC-12 – Register Student
- **Action:** Register `2023-0100 / Lab Four Tester / tester@campus.edu`, then press Register again with the same number. Earlier: blank number, and duplicate `2023-005`.
- **Expected:** Exactly one normalized-unique student, eligible, after a restart. Rejected requests add nothing.
- **Observed:** The second attempt gave orange *"Student number "2023-0100" is already registered (numbers are compared ignoring upper/lower case and spaces). Nothing was saved."* `restart-1/2/3` each show **exactly one** `2023-0100` row (Id 6, eligible `t`). Blank number gave *"Student number is required."* PostgreSQL also rejects `' 2023-0001 '` directly (`IX_Students_StudentNumberKey`, A17 Test 2).
- **Evidence:** `a12-register-student`, `a12-reject-blank-number`, `a12-reject-duplicate-number`, `seq1-duplicate-student-number-rejected`, `restart-1…3`; tests `RegisterStudent_*` (3), `Database_RejectsDuplicateStudentNumber_IgnoringCaseAndSpaces`.

## A13 / UC-13 – Update Student Information
- **Action:** Find a student with the partial search, select them, edit and save. Clear the tester's contact. A duplicate number and a missing student are covered by tests.
- **Observed:** *"Details of 2023-0100 - Lab Four Tester were updated."* `restart-1/2/3` show `ContactInfo` empty (NULL) on the same Id 6. Dan's edit kept his Id and borrowings. Search "ana" found Ana, and "zzz" showed the no-match text.
- **Evidence:** `a13-find-student-partial-search`, `a13-find-student-no-match`, `a13-update-student`, `seq1-student-contact-cleared`, readbacks; tests `UpdateStudent_ChangesDetailsAndKeepsBorrowingHistory` (the borrowing count for the student is unchanged), `UpdateStudent_NumberOfAnotherStudent_IsRejected`, `UpdateStudent_KeepingItsOwnNumber_IsAllowed`, `UpdateStudent_MissingStudent_ReturnsNotFound`.

## A14 / UC-14 – Change Student Borrowing Eligibility
- **Action:** Restrict the tester while #11 is active, try a new borrow, return #11, permit again, then do a valid checkout. Also restrict a student who is already restricted.
- **Observed:** *"Lab Four Tester is now restricted from borrowing. Existing active borrowings are not affected…"* The history still shows #11 Active. The new borrow was rejected. The return succeeded while restricted. *"Lab Four Tester is now eligible…"*, and then the checkout of #12/#13 succeeded. The repeat gave blue *"Dan Lim is already restricted. No changes were made."* The final readbacks show `IsAllowedToBorrow = t` and the linked rows #11–#13.
- **Evidence:** `seq4-restrict-keeps-active-borrowing`, `seq4-restricted-borrow-rejected`, `seq4-return-while-restricted`, `seq5-permit-and-student-history`, `a14-*`; tests `RestrictStudent_BlocksNewBorrowingButKeepsActiveBorrowings`, `PermitStudent_AllowsBorrowingAgain`, `ChangeEligibility_ToTheSameStatus_ReturnsNoChange`, `Return_ByRestrictedStudent_IsStillAllowed`.

## A15 / UC-15 – Register Equipment
- **Action:** Register `EQ-CAM-02` and `EQ-TRI-01`, then press Register again for `EQ-TRI-01`. Earlier: duplicate `eq-spk-01` and blank fields.
- **Observed:** Both items were saved as Available. The repeat gave orange *"Equipment code "EQ-TRI-01" is already registered (codes are compared ignoring upper/lower case and spaces). Nothing was saved."* `restart-1/2/3` show exactly one `EQ-TRI-01` (Id 8, `t`, never borrowed).
- **Evidence:** `seq2-register-tripod`, `seq2-duplicate-equipment-code-rejected`, `a15-register-equipment`, `a15-reject-duplicate-code`, readbacks; tests `RegisterEquipment_*` (3), `Database_RejectsDuplicateEquipmentCode_IgnoringCaseAndSpaces`.

## A16 / UC-16 – Update Equipment Information
- **Action:** Clear Camera 02's description and rename it to "Camera 02 Sony". Edit the borrowed Laptop 01. Try a blank name. Availability cannot be typed in, because the form has no such field and the service has no such parameter.
- **Observed:** *"Details of Camera 02 Sony (EQ-CAM-02) were updated. Availability was not changed."* The readbacks show Id 9 named "Camera 02 Sony" with `Description` NULL. After editing, Laptop 01 stayed **Borrowed** and kept both history rows. A blank name gave *"Equipment name is required."*
- **Evidence:** `seq2-camera02-description-cleared`, `seq2-camera02-renamed`, `a16-edit-borrowed-item-availability-unchanged`, `a16-reject-blank-name`, readbacks; tests `UpdateEquipment_ChangesDetailsButNeverAvailabilityOrHistory`, `UpdateEquipment_CodeOfAnotherItem_IsRejected`, `UpdateEquipment_MissingItem_ReturnsNotFound`.

## A17 – Migrations on a clean database and database constraints
- **Setup:** A new disposable database `equipmentborrowing_clean`, dropped afterwards.
- **Action:** `dotnet ef database update`, then inspect the schema and migration history, then send invalid SQL directly with `psql` (`docs/lab4/sql/constraint-demo.sql`), then read back over a new connection (`readback.sql`).
- **Observed:**
  - *Applying migration '20261111045753_InitialPostgreSqlSchema'. Done.* `__EFMigrationsHistory` has 1 row (EF 10.0.12).
  - `\d` shows the primary keys, both `ON DELETE RESTRICT` foreign keys, the unique indexes `IX_Students_StudentNumberKey` and `IX_Equipment_EquipmentCodeKey` on the generated columns `upper(btrim(...))`, the partial unique index `IX_Borrowings_OneActivePerEquipment … WHERE Status = 'Active'`, and the check constraints.
  - Every invalid write was refused: missing student (FK), `'  2023-0001 '` and `' eq-lap-02 '` (unique), a second active borrowing of Laptop 01 (partial unique index), expected date = checkout date (check), blank name, NULL code, Returned without a date, and deleting a student with history (RESTRICT).
  - Test 9 rolled back as a whole, and the fresh readback shows only the 4 setup rows.
- **Evidence:** `terminal-output/clean-database-migration-and-constraints.txt`; tests `DatabaseConstraintTests` (9).

## A18 – Failure, recovery, architecture
- **Controlled failure:** the app was started with `ConnectionStrings__EquipmentBorrowing` pointing to port 1 (no server), only for that run. The startup banner and every screen show a clear red message with Retry. Nothing claims success, and nothing is written.
- **Recovery:** the next normal start (restart session 1) loaded everything and saved changes normally, see `restart-1`.
- **Unknown commit outcome:** see A06. The message says UNKNOWN, there is no auto-retry, and staff refresh and check. The state was found to be all-or-nothing.
- **Code tests:** `ConnectionFailureTests`. Load returns an error result, not an exception, and that result is not "not found". A save says "Nothing was saved". A network error is classified as a connection failure. A lost commit gives UNKNOWN.
- **Architecture, context lifetimes, transaction boundary:** explained in the README section *"Architecture, DbContext lifetime and transactions"*. The code to inspect is `App.axaml.cs` (composition root), `InfrastructureServices.cs`, `EfUnitOfWork.cs` and `BorrowEquipmentService.IssueAsync`.
- **Evidence:** `a18-offline-*` (5), `restart-1`, `ConnectionFailureTests`.

## A19 – Documentation review
The use case diagram and catalog ([`use-case-catalog.md`](use-case-catalog.md)), the suite ([`use-case-suite.md`](use-case-suite.md)), the traceability matrix ([`traceability-matrix.md`](traceability-matrix.md)), this file and the README setup steps all describe the same screens, services and constraints as the source code. The Git history is in the repository (`git log`). A copy of the last 40 commits is at the end of `clean-database-migration-and-constraints.txt`.

---

## Integrated demonstration sequence (all on 2026-11-11, three separate app sessions)

| Step | What was done | Result | Read back / evidence |
|---|---|---|---|
| 1 Student foundation | Registered 2023-0100, a duplicate attempt was rejected, contact cleared | One student, updated | `seq1-*`, `restart-1` Students Id 6 |
| 2 Equipment foundation | Registered EQ-CAM-02 and EQ-TRI-01, a duplicate attempt was rejected, Camera 02 renamed and description cleared | Two available items, revised fields | `seq2-*`, `restart-1` Equipment Id 8, 9 |
| 3 Cross-workflow borrowing | Borrowed the new Camera 02 for the new student | #11 Active, Camera 02 `f` | `seq3-borrow…`, `restart-1` |
| *restart* | App closed, `psql` readback, app started again | #11 still active | `seq3-active-after-restart` |
| 4 Eligibility and return | Restricted the tester, new borrow rejected, #11 returned | #11 Returned 2026-11-11, Camera 02 available | `seq4-*`, `restart-2` |
| 5 Coordinated checkout | Permitted the tester, checked out Camera 02 + Speaker 01 | #12, #13 Active, both items `f` | `seq5-*`, `restart-2`, `restart-3` |
| 6 Invalid and failed checkout | Eve: Camera 02 (unavailable) + Tripod 01 → rejected. In-transaction failure → rolled back | No row for Eve, Tripod still `t`. Rolled-back attempt left nothing (#6 gap, test, SQL Test 9) | `seq6-*`, `restart-2`, A06 |
| 7 Related histories | Tester history and Camera 02 history | The same #11 Returned 2026-11-11 in both | `seq5-permit-and-student-history`, `seq7-*` |
| 8 Retrieval controls | Search "tester" + Returned, plus the A09 runs | #11 only | `seq8-*`, `a09-*` |
| 9 Restart and recovery | Third session: catalog and student history unchanged. Offline run and recovery | Same as `restart-2` | `seq9-*`, `restart-3`, `a18-offline-*` |
