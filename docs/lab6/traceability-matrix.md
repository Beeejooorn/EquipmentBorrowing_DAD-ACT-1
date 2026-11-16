# Lab 6 requirements traceability matrix

Execution date: 2026-11-17 (Asia/Manila). Result column: **Passed** only for tests that were actually run and passed with their assertions enabled; the final complete run is `evidence/run-06-final-56-tests-passed.txt` (56 total, 56 passed, 0 failed). Test names are in the form `Class.Method` inside `EquipmentBorrowing/tests/EquipmentBorrowing.Lab6.Tests`.

## UC → BR → API → TC6 → test → result → evidence

| UC | BR | API | TC6 | Actual test implementation | Execution result | Evidence |
|---|---|---|---|---|---|---|
| UC-01 | BR-05–07,10,19 | API-01 | U01, E07 | `CatalogTests.TC6_U01_*` (2 tests); `TransportTests.TC6_E07a_*` (desktop read when API is unreachable uses `DesktopApi.GetAllStudentsAsync`, which shares the same `GetListAsync` code path as the equipment catalog) | Passed | run-06 |
| UC-02 | BR-10,19 | API-01 | U02 | `CatalogTests.TC6_U02_*` (3 tests) | Passed | run-06 |
| UC-03 | BR-08–11,14–16,19 | API-10, 13 | U03, E02, E03, E05, E07, E08 | `BorrowingFlowTests.TC6_U03_*`; `ValidationEdgeTests.TC6_E02_S3_with_two_active_can_borrow_*`, `TC6_E03_*`; `ConflictAndRollbackTests.TC6_E05_*`; `PersistenceTests.TC6_E08_*`. E07 exercises checkout (API-11) only, not API-10, see limits. | Passed | run-06 |
| UC-04 | BR-14,19 | API-14 | U04, U09 | `BorrowingFlowTests.TC6_U04_*`; `HistoryAndSearchTests.TC6_U09_*` (8 tests) | Passed | run-06 |
| UC-05 | BR-10,13–14,19 | API-12, 13 | U05, U14, E08 | `BorrowingFlowTests.TC6_U05_*`; `ManagementTests.TC6_U14_*`; `PersistenceTests.TC6_E08_*` | Passed | run-06 |
| UC-06 | BR-08–12,14–16,19 | API-11 | U06, E02–E05, E07 | `BorrowingFlowTests.TC6_U06_*`; `ValidationEdgeTests.TC6_E02_*`, `TC6_E03_*`; `ConflictAndRollbackTests.TC6_E04_*`, `TC6_E05_*`; `TransportTests.TC6_E07a/b/c_*` | Passed | run-06 |
| UC-07 | BR-14,19 | API-03, 15 | U07 | `HistoryAndSearchTests.TC6_U07_*` (3 tests) | Passed | run-06 |
| UC-08 | BR-14,19 | API-07, 16 | U08, U16 | `HistoryAndSearchTests.TC6_U08_*` (3 tests); `ManagementTests.TC6_U16_*` | Passed | run-06 |
| UC-09 | BR-17,19 | API-14 | U09, E03 | `HistoryAndSearchTests.TC6_U09_*`; `ValidationEdgeTests.TC6_E03_*` (invalid query values) | Passed | run-06 |
| UC-12 | BR-01–02,16,19 | API-03, 04 | U12, E01, E08 | `ManagementTests.TC6_U12_*`; `ValidationEdgeTests.TC6_E01_Student_*`; `PersistenceTests.TC6_E08_*` | Passed | run-06 |
| UC-13 | BR-01–03,14,16,19 | API-02, 03, 05, 15 | U13, E01, E06 | `ManagementTests.TC6_U13_*`; `ValidationEdgeTests.TC6_E01_Student_*`, `TC6_E06_*` (3 tests) | Passed | run-06 |
| UC-14 | BR-04,08,13–14,19 | API-06, 10, 12 | U14 | `ManagementTests.TC6_U14_*` | Passed | run-06 |
| UC-15 | BR-05–06,10,16,19 | API-07, 08 | U15, E01, E08 | `ManagementTests.TC6_U15_*`; `ValidationEdgeTests.TC6_E01_Equipment_*`, `TC6_E01_PostgreSQL_*`; `PersistenceTests.TC6_E08_*` | Passed | run-06 |
| UC-16 | BR-05–07,10,14,16,19 | API-07, 09, 16 | U16, E01, E06 | `ManagementTests.TC6_U16_*`; `ValidationEdgeTests.TC6_E01_Equipment_*`, `TC6_E06_*` | Passed | run-06 |

Manual acceptance checks MA6-01 (desktop responsiveness) and MA6-02 (independent API and synchronization) cover UC-01–09, 12–16 and BR-14, 18–19 in the desktop UI. Their status is in [test-execution-record.md](test-execution-record.md) and the procedure in [manual-acceptance.md](manual-acceptance.md).

## API coverage check (every API exercised through the real HTTP pipeline)

| API contract | Automated scenario(s) | Actual HTTP test / result / evidence |
|---|---|---|
| API-01 `GET /api/v1/equipment` | U01, U02 | `CatalogTests` — Passed (run-06) |
| API-02 `GET /api/v1/students` | U13 (also used to resolve IDs in every test) | `ManagementTests.TC6_U13_*` — Passed |
| API-03 `GET /api/v1/students/{id}` | U07, U12, U13 | `HistoryAndSearchTests.TC6_U07_Student_by_id_*`, `ManagementTests.TC6_U12_*`, `TC6_U13_*` — Passed |
| API-04 `POST /api/v1/students` | U12, E01 | `ManagementTests.TC6_U12_*`, `ValidationEdgeTests.TC6_E01_*` — Passed |
| API-05 `PUT /api/v1/students/{id}` | U13, E01, E06 | `ManagementTests.TC6_U13_*`, `ValidationEdgeTests.TC6_E01_*`, `TC6_E06_*` — Passed |
| API-06 `PATCH /api/v1/students/{id}/eligibility` | U14 | `ManagementTests.TC6_U14_*` — Passed |
| API-07 `GET /api/v1/equipment/{id}` | U08, U15 (follows Location) | `HistoryAndSearchTests.TC6_U08_Equipment_by_id_*`, `ManagementTests.TC6_U15_*` — Passed |
| API-08 `POST /api/v1/equipment` | U15, E01 | `ManagementTests.TC6_U15_*`, `ValidationEdgeTests.TC6_E01_*` — Passed |
| API-09 `PUT /api/v1/equipment/{id}` | U16, E01, E06 | `ManagementTests.TC6_U16_*`, `ValidationEdgeTests.TC6_E01_*`, `TC6_E06_*` — Passed |
| API-10 `POST /api/v1/borrowings` | U03, U14, E02, E03, E05, E08 | `BorrowingFlowTests.TC6_U03_*`, `ManagementTests.TC6_U14_*`, `ValidationEdgeTests.TC6_E02_*`, `TC6_E03_*`, `ConflictAndRollbackTests.TC6_E05_*`, `PersistenceTests.TC6_E08_*` — Passed |
| API-11 `POST /api/v1/checkout-requests` | U06, E02–E05, E07 | `BorrowingFlowTests.TC6_U06_*`, `ValidationEdgeTests.TC6_E02_*`, `TC6_E03_*`, `ConflictAndRollbackTests.*`, `TransportTests.*` — Passed |
| API-12 `POST /api/v1/borrowings/{id}/return` | U05, U14, E08 | `BorrowingFlowTests.TC6_U05_*`, `ManagementTests.TC6_U14_*`, `PersistenceTests.*` — Passed |
| API-13 `GET /api/v1/borrowings/{id}` | U03, U05, E08 (restart harness) | `BorrowingFlowTests.TC6_U03_*`, `TC6_U05_*`, `PersistenceTests.TC6_E08_Real_API_*` — Passed |
| API-14 `GET /api/v1/borrowings` | U04, U09, E03 | `BorrowingFlowTests.TC6_U04_*`, `HistoryAndSearchTests.TC6_U09_*`, `ValidationEdgeTests.TC6_E03_*` — Passed |
| API-15 `GET /api/v1/students/{id}/borrowings` | U07, U13 | `HistoryAndSearchTests.TC6_U07_*`, `ManagementTests.TC6_U13_*` — Passed |
| API-16 `GET /api/v1/equipment/{id}/borrowings` | U08, U16 | `HistoryAndSearchTests.TC6_U08_*`, `ManagementTests.TC6_U16_*` — Passed |

## Limits that apply to this matrix

* TC6-E07 is automated through the real `DesktopApi` class and a controlled HTTP test double or the real API; it exercises checkout (API-11). Single borrowing (API-10) transport failures use the same `DesktopApi.SendAsync` code path, but no separate API-10 transport test was written.
* TC6-E07c uses the application's simulated lost-acknowledgement. It proves client behavior and that the commit really happened (fresh PostgreSQL read). It is not a real network drop.
* TC6-E08: the "API restart" is evidenced by an integration harness (`PersistenceTests.TC6_E08_Real_API_process_restart_*`) that starts the built `EquipmentBorrowing.Api.dll` as a separate process on port 5188, stops it, shows that nothing answers, starts a new process and compares the retrieved records with a fresh PostgreSQL read. No supervised manual restart was needed.
* D equals the run date (see the baseline record).
