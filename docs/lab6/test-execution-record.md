# Lab 6 test execution record

Environment: Windows, .NET 10 (xUnit VSTest adapter on .NET 10.0.12), Visual Studio solution `EquipmentBorrowing.slnx`, local PostgreSQL, disposable database `equipmentborrowing_lab6_test`. D = run date, 2026-11-17 (Asia/Manila). Command for every run: `dotnet test tests\EquipmentBorrowing.Lab6.Tests` from the `EquipmentBorrowing` folder.

Statuses are exactly Passed, Failed, Blocked or Not Run. Passed means the test actually ran with its assertions enabled and was satisfied in the run named; nothing is inferred from code.

## Runs

| Run | What was added | Total / Passed / Failed | Notes | Evidence |
|---|---|---|---|---|
| 1 | Test project, fixtures, isolation, API start (5 setup tests) | 5 / 5 / 0 | Infrastructure check (screenshot seen by the assistant, no text file kept) | — |
| 2 | TC6-U01–U09, OpenAPI contract | 32 / 32 / 0 | | [run-02](evidence/run-02-32-tests-passed.txt) |
| 3 | TC6-U12–U16 | 37 / 36 / 1 | U16 failed because the test read a field called `borrowingId`; the API field is `id`. Test-script error, corrected. | [run-03](evidence/run-03-37-tests-1-test-script-error.txt) |
| 4 | TC6-E01–E07 | 54 / 53 / 1 | TC6-E03 failed: application defect D-01 | [run-04](evidence/run-04-54-tests-E03-defect-D01.txt) |
| 5 | Fix D-01 + retest | 54 / 54 / 0 | Reported by the student after the fix; no output file was kept for this intermediate run | — |
| 6 (final) | TC6-E08 | 56 / 56 / 0 | Full regression of everything | [run-06](evidence/run-06-final-56-tests-passed.txt) |

## Results by test case

| Test Case ID | Actual test implementation / manual method | Expected result | Observed result | Execution status | Defect reference | Correction | Retest result | Evidence location |
|---|---|---|---|---|---|---|---|---|
| TC6-U01 | CatalogTests.TC6_U01_* (2) | 200, 7 items sorted by code, correct availability; empty DB gives []; no writes | As expected; snapshot unchanged | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U02 | CatalogTests.TC6_U02_* (3) | available=true lists E2, E3, E6, E7 only; hides items with an active borrowing; [] when none | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U03 | BorrowingFlowTests.TC6_U03_* | S4/E2 due D+2: 201, Location, Active; E2 unavailable in fresh DB | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U04 | BorrowingFlowTests.TC6_U04_* | Active list = E1, E4, E5 sorted by return date then ID; returned E3 excluded | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U05 | BorrowingFlowTests.TC6_U05_* | Return gives 200 Returned with date D; repeat gives 409 and changes nothing | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U06 | BorrowingFlowTests.TC6_U06_* | Checkout S4/E2+E6: 201, two active rows, both items unavailable | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U07 | HistoryAndSearchTests.TC6_U07_* (3) | S1 history = active E1 + returned E3 only; empty history 200 []; missing student 404 | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U08 | HistoryAndSearchTests.TC6_U08_* (3) | E1 active/S1, E3 returned/S1, E2 empty; missing item 404 | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U09 | HistoryAndSearchTests.TC6_U09_* (8) | Search by name/number/equipment, status filters, both sorts with ID tie-break, 400 for invalid values | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U12 | ManagementTests.TC6_U12_* | POST ST-500: 201, ID, Location, eligible=true, one durable row | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U13 | ManagementTests.TC6_U13_* | API-02 match and no-match; PUT changes name/contact; same ID; history FKs unchanged | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U14 | ManagementTests.TC6_U14_* | Restrict 200; issue 409; return 200; permit 200; issue 201 | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U15 | ManagementTests.TC6_U15_* | EQ-008: 201 available=true; duplicate EQ-001: 409; no duplicate row | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-U16 | ManagementTests.TC6_U16_* | Edit E1 description: 200, still unavailable, original active record kept | Run 3: test failed because the TEST used the wrong JSON field name; corrected and re-run, as expected in run 6 | Passed (after test-script correction) | — (test-script mistake, not an application defect) | Test script fixed (field `id`) | Run 6 passed | run-06-final-56-tests-passed.txt |
| TC6-E01 | ValidationEdgeTests.TC6_E01_* (3) | Case/space variants 409 for create and update; PostgreSQL itself rejects duplicates (SQLSTATE 23505); counts unchanged | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-E02 | ValidationEdgeTests.TC6_E02_* (3) | S3+E2 single → exactly 3; S1+E2/E6 checkout → exactly 3; S3+E2/E6 checkout → 409 with no partial writes | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-E03 | ValidationEdgeTests.TC6_E03_* (3) | 400 invalid, 404 missing/unknown, 405 wrong method, 409 conflict; safe bodies; database unchanged | Run 4: invalid-JSON response body contained internal exception text (defect D-01). After the fix, run 5/6: as expected | Passed (after fix of D-01) | D-01 | Middleware returns a safe 400 (see defect-record.md) | Run 5 and run 6 passed | run-06 (and run-04 for the failure) |
| TC6-E04 | ConflictAndRollbackTests.TC6_E04_* | Fault after first real write → safe 500; E2/E6 and all rows identical to baseline on a fresh connection | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-E05 | ConflictAndRollbackTests.TC6_E05_* | First issue 201; stale single and checkout 409; PostgreSQL rejects a second active E2 row (23505) | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-E06 | ValidationEdgeTests.TC6_E06_* (3) | Optional fields cleared to null; blank/duplicate/isAvailable edits refused (400/409); availability and history intact | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-E07 | TransportTests.TC6_E07a / E07b / E07c | E07a no HTTP response, one attempt; E07b received 500, safe message, no partial writes; E07c unknown outcome, one POST only, fresh DB shows all-or-none | As expected | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| TC6-E08 | PersistenceTests.TC6_E08_* (2) | Fresh-connection state after create, issue, return; real API process stopped and restarted, same records retrieved and equal to PostgreSQL | As expected (restart harness method, port 5188) | Passed | — | — | — | run-06-final-56-tests-passed.txt |
| MA6-01 | Manual: delayed response, stop/restore API on the desktop UI | Responsive UI, loading state, understandable unreachable-API message, no unintended writes | Loading state shown; window usable (student report); timeout message after 30 s; "Cannot reach the API" message when stopped; catalog recovered after API restart; counts 4/7/4 before and after | Passed (2026-11-17) | - | - | - | manual-acceptance.md, evidence/ma6-01-*.png |
| MA6-02 | Manual: API without desktop, OpenAPI check, independent client change, desktop refresh, source inspection | Independent API works; OpenAPI consistent; UI refreshes; desktop is HTTP-only | API answered in a browser; OpenAPI has 16 operations; PATCH from PowerShell restricted ST-100 and the desktop showed Restricted; Desktop project has no database packages and uses HttpClient | Passed (2026-11-17) | - | - | - | manual-acceptance.md, evidence/ma6-02-*.png |

Additional automated checks (not TC6 objectives): 5 setup tests (`FixtureAndIsolationTests`) and 3 OpenAPI contract tests (`OpenApiContractTests`). Counted in the 56.

Totals for the TC6 objectives: 22 automated objectives (U01-U09, U12-U16, E01-E08) Passed in the final run; MA6-01 and MA6-02 Passed (manual, 2026-11-17).
