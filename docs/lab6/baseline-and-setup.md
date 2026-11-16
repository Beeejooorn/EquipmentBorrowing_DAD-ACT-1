# Lab 6 baseline record and test setup

Submission members: Hervy Bjorn C. Tanamal (2022302797) and Steven Andujar Malnegro (2022301783), as also recorded in [`Student.cs`](../../EquipmentBorrowing/src/EquipmentBorrowing.Domain/Student.cs).

## Implementation baseline record

| Field | Entry |
|---|---|
| Authentic Lab 5 submitted commit / tag | The existing Git tag `lab5-submitted` points to commit `8e99d18741a8972ff2548ba966f6a09c842f06ca` (the commit "Record duplicate-update…" in the history). Lab 6 work starts on top of it; the tagged commit was not changed. |
| How the baseline was verified; missing history or limitations | The tag file `.git/refs/tags/lab5-submitted` was read directly and its commit ID is shown above; the History view of GitHub Desktop shows the tag on that commit. Lab 6 commits are made after it on branch `main`. No history was rewritten or invented. Limitation: the Lab 4 → Lab 5 baseline details stay as already recorded in [`../lab5/acceptance-record.md`](../lab5/acceptance-record.md). |
| Final Lab 6 commit and `lab6-submitted` tag | The `lab6-submitted` tag is on the last commit of the Lab 6 work (the commit "Finish Lab 6 documentation and baseline notes"). A commit cannot contain its own ID, so look at the History tab in GitHub Desktop or the Tags page on GitHub to see the exact commit. |
| Test project(s), isolated PostgreSQL setup, fixture/reset and D | Test project: `EquipmentBorrowing/tests/EquipmentBorrowing.Lab6.Tests` (xUnit 2.9.3, `Microsoft.AspNetCore.Mvc.Testing`), added to `EquipmentBorrowing.slnx`. The earlier Lab 4 test project (`tests/EquipmentBorrowing.Tests`) is unchanged. Database: real PostgreSQL, a separate disposable database named `equipmentborrowing_lab6_test` (the code refuses to reset any database whose name does not end in `_lab6_test`). Reset: before every test the tables are emptied (`TRUNCATE … RESTART IDENTITY CASCADE`) and the fixtures S1–S4 and E1–E7 plus four borrowings are inserted again, so every test is independent. **D = the calendar date of the run** (the application itself uses `DateTime.Now`, so D cannot be fixed to another date); fixture dates are D−9 … D+2. If a run crosses midnight, run it again. |
| Significant refactoring and reproducible execution instructions | Only one production-code change was needed to start the real API from tests: `public partial class Program { }` was appended to `Program.cs` (no behavior change). One defect fix was made (D-01, see [defect-record.md](defect-record.md)). Run instructions are in the repository [README](../../README.md#laboratory-activity-6-automated-verification-and-regression-testing). |

## How the tests reach the real system

* **Real HTTP pipeline.** `LabApiFactory` (`WebApplicationFactory<Program>`) starts the real API (same `Program.cs`, routing, binding, services and EF Core) inside the test process. Each test sends real HTTP requests with `HttpClient`.
* **Real PostgreSQL.** The API's connection string is overridden with the environment variable `ConnectionStrings__EquipmentBorrowing` so that it uses the test database. No SQLite and no EF InMemory are used.
* **Fresh independent readback.** Test assertions on the database use a new `DbContext` with `Pooling=false`, so each read is a new PostgreSQL connection, never the one the API used.
* **Direct database attempts.** `LabDatabase.TryRawSql` sends SQL straight to PostgreSQL to prove the unique indexes reject duplicates (SQLSTATE 23505) even when the API is bypassed (TC6-E01, TC6-E05).
* **Controlled fault.** The existing `CheckoutFailureSimulator` (already part of the application since Lab 4/5) is switched on at run time inside a test to cause a failure after the first real write (TC6-E04, E07b) or a lost commit acknowledgement (TC6-E07c). It is off by default.
* **No parallelism.** Test classes run one after another so that they never share the single test database at the same time.
* **No credentials in the repository.** The connection string comes from `dotnet user-secrets` or the environment variable, exactly like the API; the test project contains none.

## Test files

| File | Contents |
|---|---|
| `Support/LabDatabase.cs` | Reset, fixtures S1–S4 / E1–E7, D, fresh readbacks, snapshot, raw SQL |
| `Support/LabApiFactory.cs`, `Support/ApiTestBase.cs` | Real API host, HTTP helpers, ID lookup through the API |
| `FixtureAndIsolationTests.cs` | Setup checks: fixtures, dates relative to D, API starts, test isolation |
| `CatalogTests.cs` | TC6-U01, U02 |
| `BorrowingFlowTests.cs` | TC6-U03 to U06 |
| `HistoryAndSearchTests.cs` | TC6-U07 to U09 |
| `ManagementTests.cs` | TC6-U12 to U16 |
| `ValidationEdgeTests.cs` | TC6-E01, E02, E03, E06 |
| `ConflictAndRollbackTests.cs` | TC6-E04, E05 |
| `TransportTests.cs` | TC6-E07a, E07b, E07c (uses the real `DesktopApi` class) |
| `PersistenceTests.cs` | TC6-E08 (fresh connections and real API process restart) |
| `OpenApiContractTests.cs` | Published OpenAPI document lists exactly the 16 operations and their success codes (supports MA6-02 and rubric R2) |
