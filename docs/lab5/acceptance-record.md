# Activity 5 acceptance record

This record maps the instructor's T01–T08 catalog to the evidence actually captured. The API and desktop implementation is in this repository. Screenshots in [`evidence/`](evidence/) are exploratory verification against `equipmentborrowing_lab5` using the server's actual local date (2026-11-15 in the captured borrowing responses). They are **not** a claim that the instructor's controlled S1–S4/E1–E7 dataset was reset and fully executed for each T case. Use [`sql/reset-acceptance-data.sql`](sql/reset-acceptance-data.sql) on a separate disposable `*_lab5_acceptance` database for that repeatable run.

| Test | Related API / use cases | Captured execution and evidence | Remaining acceptance steps |
|---|---|---|---|
| T01 | API-01–03, 07, 13–16; UC-01/02/04/07/08/09 | Independent HTTP list/detail, available filter, missing IDs, empty histories, and OpenAPI GET 200 / 16 operations: `evidence/api01-*`, `api02-*`, `api03-*`, `api07-*`, `api13-*` through `api16-*`, `openapi-*`. | Run against reset S1–S4/E1–E7; capture wrong route 404, unsupported method 405, full OpenAPI schema/status comparison, and before/after read. |
| T02 | API-04–06; UC-12–14; BR-01–04 | Student registration/update/eligibility, duplicate and invalid input, independent readback: `evidence/api04-*`, `api05-*`, `api06-*`; restricted borrow: `api10-09-restricted-student-http-409.png`. | Reset instructor seed; execute the exact ST-500/S1 flow, repeat eligibility, restart, and fresh before/after database reads. |
| T03 | API-07–09; UC-15–16; BR-05–07 | Equipment registration/update, duplicate and protected availability, independent readback: `evidence/api08-*`, `api09-*`. | Reset instructor seed; execute exact EQ-008/E1 flow and verify history and state across restart. |
| T04 | API-10, 12–13; UC-03/05; BR-08–11/13 | Single issue, unavailable/restricted/invalid/missing rejections, return, repeat return, borrowing lookup, and PostgreSQL readback: `evidence/api10-*`, `api12-*`, `api13-*`. | Reset instructor seed; execute S4/E2 flow and verify the same record after API restart. |
| T05 | API-10–11; UC-03/06; BR-08–12/15–16 | Two-item HTTP 201 and PostgreSQL rows; over-limit/unavailable HTTP 409; duplicate/single/missing-date HTTP 400; missing item HTTP 404; rejected request PostgreSQL readback: `evidence/api11-*`. | Reset before each 2a/2b/2c subcase; execute exact S3/S1 boundaries, restricted S2 and invalid-date subcases; induce a controlled failure **after a write attempt** and capture HTTP 500 plus fresh unchanged PostgreSQL rows. Current rejection evidence alone does not prove the required fault rollback. |
| T06 | API-14–16; UC-04/07–09; BR-14/17 | Student/equipment histories, empty/missing responses, search/filter/sort/no-match/invalid status: `evidence/api14-*`, `api15-*`, `api16-*`. | Reset instructor seed; cover mixed-case student number/name search, both sort fields in both directions, combined filters and reset, with fresh before/after read. |
| T07 | API-01–16; UC-01–09/12–16; architecture | Desktop has no Infrastructure reference; independent API, catalog/active reads, return, and catalog availability refresh: `evidence/desktop-01-*` through `desktop-06-*`. | On controlled seed, register/edit student and equipment in desktop, issue/return, view histories, change data by independent HTTP client and refresh desktop, and capture database readback. |
| T08 | API-01–16; BR-18/19 | Desktop API outage message/recovery; separate reachable API with invalid DB produced safe HTTP 500, desktop displayed server-error message, then reopened against working API: `evidence/desktop-03-*`, `desktop-04-*`, `t08-*`. | Capture direct HTTP transport failure with no response, server logs without secrets, and fresh PostgreSQL read of committed data across restart. |

## Source and baseline

The pre-Activity-5 repository revision immediately before the independent API host is `1e9685a`; Activity 5 starts at `cde0d70`. `1e9685a` is a verifiable repository baseline, but the original submitted Lab 4 revision has not been independently verified, so no `lab4-submitted` tag has been created. The completed Activity 5 revision and `lab5-submitted` tag are pending the remaining acceptance steps.

The functional baseline is [`../lab4/use-case-suite.md`](../lab4/use-case-suite.md) and [`../lab4/traceability-matrix.md`](../lab4/traceability-matrix.md). Current API routes and response mapping are in [`../../EquipmentBorrowing/src/EquipmentBorrowing.Api/Program.cs`](../../EquipmentBorrowing/src/EquipmentBorrowing.Api/Program.cs); business rules remain in `EquipmentBorrowing.Application/Services`, persistence and migrations in `EquipmentBorrowing.Infrastructure`, and the HTTP desktop adapter in [`../../EquipmentBorrowing/src/EquipmentBorrowing.Desktop/Services/DesktopApi.cs`](../../EquipmentBorrowing/src/EquipmentBorrowing.Desktop/Services/DesktopApi.cs). The published OpenAPI document is [`../../EquipmentBorrowing/src/EquipmentBorrowing.Api/OpenApi/v1.json`](../../EquipmentBorrowing/src/EquipmentBorrowing.Api/OpenApi/v1.json).

## API-to-use-case traceability

| Use cases | Endpoints | Implementation | Evidence |
|---|---|---|---|
| UC-01/02 | API-01 equipment list and availability | `Program.cs` equipment GET; `EquipmentQueryService` | `evidence/api01-*`, `desktop-01-*` |
| UC-12/13/14 | API-02–06 student list, lookup, create, edit, eligibility | `Program.cs` student routes; `StudentQueryService`, `StudentManagementService` | `evidence/api02-*` through `api06-*` |
| UC-15/16 | API-07–09 equipment lookup, create, edit | `Program.cs` equipment routes; `EquipmentQueryService`, `EquipmentManagementService` | `evidence/api07-*` through `api09-*` |
| UC-03/06 | API-10–11 single borrow and checkout | `Program.cs` borrowing POST routes; `BorrowEquipmentService` | `evidence/api10-*`, `api11-*` |
| UC-05 | API-12 return | `Program.cs` return POST; `ReturnEquipmentService` | `evidence/api12-*`, `desktop-05-*`, `desktop-06-*` |
| UC-04/07/08/09 | API-13–16 borrowing detail, search, student and equipment histories | `Program.cs` borrowing GET routes; `BorrowingQueryService` | `evidence/api13-*` through `api16-*`, `desktop-02-*` |

The desktop calls the same endpoint set through `IDesktopApi`; it has no direct database connection. Changes from Lab 4 are the separate API host, published HTTP contracts, HTTP desktop adapter, and removal of desktop database startup and its local checkout fault controls. Database transaction rules remain in the application and infrastructure layers.
