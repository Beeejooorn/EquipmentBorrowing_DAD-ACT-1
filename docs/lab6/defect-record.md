# Lab 6 defect and regression record

One application defect was found by the automated tests. One earlier failure was a mistake in a test script and is listed separately so the record stays honest.

## D-01 — Malformed JSON request body exposes internal exception text

| Field | Entry |
|---|---|
| Defect ID and affected TC6 / UC / BR / API | **D-01**. Found by TC6-E03 (protocol errors). Related: UC-03, 06, 09, 12, 15; BR-17–18 (safe errors); API-04, 08, 10, 11 (every API that reads a JSON body). Demonstrated on API-04 `POST /api/v1/students`. |
| Environment, deterministic data / D, reproduction | Windows, .NET 10, real API in Development mode (the mode set by `launchSettings.json`), PostgreSQL test database `equipmentborrowing_lab6_test`, fixtures S1–S4/E1–E7, D = 2026-11-17. Reproduce: send `POST /api/v1/students` with the body `{ this is not json` and `Content-Type: application/json`. Automated: `ValidationEdgeTests.TC6_E03_Invalid_requests_are_400_and_change_nothing`. |
| Expected vs observed; likely layer and diagnostic evidence | **Expected:** HTTP 400 with a short safe message in the API's usual `{code, message}` shape and no internal details. **Observed:** HTTP 400, but the response body was the framework's developer error text, beginning with `Microsoft.AspNetCore.Http.BadHttpRequestException: Failed to read parameter "CreateStudentRequest request" from the request body as JSON.`, i.e. an exception type name and details. **Layer:** API request binding (no middleware converted the framework's `BadHttpRequestException` into the API's safe error format; in Development ASP.NET Core then shows its developer page). Business rules and database were not involved. **Evidence:** [run-04 excerpt](evidence/run-04-54-tests-E03-defect-D01.txt): `Assert.DoesNotContain() Failure … Found: "Exception"`. |
| Correction or reason unresolved; revision | **Corrected.** `Program.cs` now has a small middleware right after `builder.Build()` that catches `BadHttpRequestException` and answers `400 {"code":"invalid_request","message":"The request could not be read. Check that the body is valid JSON with the expected fields."}`. Revision: git commit "Fix D-01: return a safe 400 for malformed JSON request bodies" (commit ID is shown in the repository history and is recorded in the final README table). |
| Targeted retest and broader regression results | **Targeted retest:** TC6-E03 passed after the fix (student-reported run 5 and the final run 6). **Broader regression:** the complete suite of 56 tests (all 22 TC6 objectives, 5 setup tests, 3 OpenAPI tests) passed in run 6 with 0 failures. |
| Fresh PostgreSQL readback / HTTP / UI / source evidence locations | The same test compares a before/after snapshot of all students, equipment and borrowings read through a fresh connection: the rejected request changed nothing. Run excerpts: `evidence/run-04-…` (failure) and `evidence/run-06-…` (final pass). Source: `src/EquipmentBorrowing.Api/Program.cs` (middleware marked "Defect fix D-01"). |

## Not an application defect: U16 test-script mistake

In run 3 `TC6_U16_Update_equipment_E1_*` failed with `KeyNotFoundException`. The cause was in the test: it read the property `borrowingId` from the equipment-history response, but the API (and the OpenAPI contract) name that field `id`. The application behaved correctly. The test was corrected and passed in later runs ([run-03 excerpt](evidence/run-03-37-tests-1-test-script-error.txt)). It is not counted as an application defect.

## Not an application defect: build warning

Run 4 showed the analyzer warning `xUnit2013` (use `Assert.Single`). It was fixed in the test code; run 6 builds with no warnings.
