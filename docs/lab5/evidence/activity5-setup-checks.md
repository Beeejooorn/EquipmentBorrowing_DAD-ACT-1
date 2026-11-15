# Activity 5 Setup Checkpoints

| Checkpoint | Result | Evidence |
|---|---|---|
| PostgreSQL client | PostgreSQL 18.6 available at `C:\Program Files\PostgreSQL\18\bin\psql.exe`; short command `psql` is not on PATH. | User-provided PowerShell output (text) |
| PostgreSQL server | `localhost:5432` accepts connections (`pg_isready`). | User-provided PowerShell output (text) |
| PostgreSQL login | `postgres` connected to the `postgres` database on localhost:5432; `SELECT version()` returned PostgreSQL 18.6. | User-provided PowerShell output (text; password omitted) |
| Lab 4 database | `equipmentborrowing` exists; confirmed by read-only query of `pg_database`. | User-provided PowerShell output (text) |
| Activity 5 database name | `equipmentborrowing_lab5` does not exist (`0 rows`), so it is available for isolated Activity 5 work. | User-provided PowerShell output (text) |
| Activity 5 database created | `CREATE DATABASE equipmentborrowing_lab5` succeeded; separate from the Lab 4 database. | User-provided PowerShell output (text) |
| EF Core command-line tool | `dotnet ef --version` returned `10.0.12`. | User-provided PowerShell output (text) |
| Existing migration | `dotnet ef migrations list` built successfully and listed `20261111045753_InitialPostgreSqlSchema`. | User-provided PowerShell output (text) |
| Activity 5 connection setting | Session-only `ConnectionStrings__EquipmentBorrowing` points to `equipmentborrowing_lab5`; temporary password variable removed. Credential value was not recorded. | User-provided PowerShell output (text) |
| Activity 5 schema migration | `dotnet ef database update` built successfully, applied `20261111045753_InitialPostgreSqlSchema`, and ended `Done.` The initial migration-history lookup logged a failed command because the new database had no history table yet. | User-provided PowerShell output (text) |
| Activity 5 tables verified | Fresh `psql` connection listed `Borrowings`, `Equipment`, `Students`, and `__EFMigrationsHistory`. | User-provided PowerShell output (text) |
| API project scaffold | `dotnet new web` created and restored `EquipmentBorrowing.Api` targeting .NET 10. | User-provided PowerShell output (text) |
| API added to solution | `EquipmentBorrowing.Api.csproj` added to `EquipmentBorrowing.slnx`. | User-provided PowerShell output (text) |
| API project references | API references Application and Infrastructure projects. | User-provided PowerShell output (text) |
| API build | `dotnet build EquipmentBorrowing/src/EquipmentBorrowing.Api/EquipmentBorrowing.Api.csproj` succeeded. | User-provided PowerShell output (text) |
| API independent startup | API reported `Now listening on: http://localhost:5085` and `Application started` with desktop closed. | `setup-06-api-running-independently.png` |
| API HTTP response | Browser received `{ "message": "Equipment Borrowing API is running." }` from `http://localhost:5085/`. | `setup-07-api-root-response.png` |
| API-01 build | Updated API with `GET /api/v1/equipment` built successfully. | User-provided PowerShell output (text) |
| API-01 empty list | `GET /api/v1/equipment` returned `[]` against the empty Activity 5 database. | `api01-03-empty-equipment-list.png` |
| API-01 invalid filter body | `available=maybe` returned `{ "code": "invalid_query", "message": "available must be true or false." }`; HTTP status still to verify. | `api01-04-invalid-filter-body.png` |
| API-01 invalid filter status | `GET /api/v1/equipment?available=maybe` returned HTTP 400 and the expected JSON error body. | `api01-05-invalid-filter-http-400.png` |
| API-01 empty-list status | `GET /api/v1/equipment` returned HTTP 200 with `[]`. | `api01-06-empty-list-http-200.png` |
| API-02 build | Updated API with `GET /api/v1/students` built successfully. | User-provided PowerShell output (text) |
| API-02 empty list | `GET /api/v1/students` returned `[]` against the empty Activity 5 database. | `api02-03-empty-student-list.png` |
| API-02/03 build | API with student list and student-by-ID routes built successfully. | User-provided PowerShell output (text) |
| API-03 missing student body | `GET /api/v1/students/999999` returned `{ "code": "not_found", "message": "Student not found." }`; HTTP status still to verify. | `api03-04-missing-student-body.png` |
| API-03 missing student status | `GET /api/v1/students/999999` returned HTTP 404; JSON body captured separately. | User-provided PowerShell output (text) and `api03-04-missing-student-body.png` |
| API-04 build | API including student registration built successfully. | `api04-04-build-succeeded.png` |
| API-04 create and API-02 readback | POST student returned HTTP 201, Location `/api/v1/students/1`, and eligible student JSON; subsequent GET students showed the same record. | `api04-05-register-student-http-201.png`, `api02-04-student-list-after-registration.png` |
| API-03 created-student readback | `GET /api/v1/students/1` returned the same student registered by API-04. | `api03-06-created-student-by-id.png` |
| API-02 student search | `GET /api/v1/students?search=Tester` returned the registered Lab Five Tester in an array. | `api02-05-search-tester.png` |
| API-04 duplicate rejection | Repeating the registration returned HTTP 409 with `duplicate_student_number` and `Nothing was saved.` | `api04-06-duplicate-student-http-409.png` |
| API-04 duplicate readback | After HTTP 409, `GET /api/v1/students` still showed exactly one `ST-LAB5-001` record. Independent PostgreSQL readback remains to be captured. | `api04-07-one-student-after-duplicate.png` |
| API-04 independent database readback | Fresh `psql` connection to `equipmentborrowing_lab5` showed exactly one eligible `ST-LAB5-001` row after duplicate rejection. | `api04-08-postgresql-one-student-after-duplicate.png` |
| API-05 build | API including student update built successfully. | `api05-03-build-succeeded.png` |
| API-05 update | PUT student 1 returned HTTP 200 with same ID, updated name/contact, and eligibility true. PostgreSQL readback pending. | `api05-04-update-student-http-200.png` |
| API-05 PostgreSQL readback | Separate `psql` session showed ID 1 with updated name/contact and eligibility unchanged. | `api05-05-postgresql-update-readback.png` |
| API-05 clear contact | PUT student 1 with `contact: null` returned HTTP 200 and `contact: null` in response; database readback pending. | `api05-06-clear-contact-http-200.png` |
| API-05 clear-contact database check | Independent PostgreSQL query showed `ContactInfo IS NULL = t` for student ID 1. | `api05-07-postgresql-contact-is-null.png` |
| API-05 missing student | PUT unknown student ID returned HTTP 404 with `not_found` JSON body. | `api05-08-missing-student-http-404.png` |
| API-05 blank-name validation | PUT student 1 with blank `fullName` returned HTTP 400 and `Full name is required.` Independent readback pending. | `api05-09-blank-name-http-400.png` |
| API-05 rejected-update readback | PostgreSQL still showed one student ID 1, updated name, and `ContactInfo IS NULL = t` after HTTP 400. | `api05-10-postgresql-unchanged-after-400.png` |
| API-06 build | API including student eligibility route built successfully. | `api06-03-build-succeeded.png` |
| API-06 restrict student | Completed PATCH returned HTTP 200 with `isEligible: false`. Screenshot also shows an earlier incomplete PowerShell command that never reached the API; only the successful retry is counted. | `api06-04-restrict-student-http-200.png` |
| API-06 PostgreSQL restriction readback | Independent PostgreSQL query showed `IsAllowedToBorrow = f` for student ID 1. | `api06-05-postgresql-restricted.png` |
| API-06 repeat restriction | Repeating PATCH `isEligible: false` returned HTTP 200 with eligibility still false; the application service has an explicit no-change branch. | `api06-06-repeat-restriction-http-200.png` |
| API-06 restore eligibility | PATCH `isEligible: true` returned HTTP 200 and eligible student JSON. | `api06-07-restore-eligibility-http-200.png` |
| API-06 PostgreSQL restoration readback | Independent PostgreSQL query showed `IsAllowedToBorrow = t` for student ID 1. | `api06-08-postgresql-eligible-again.png` |
| API-06 missing eligibility value | PATCH with `{}` returned HTTP 400 and `invalid_eligibility`. | `api06-09-missing-value-http-400.png` |
| API-06 unknown student | PATCH unknown student ID returned HTTP 404 with `not_found`. | `api06-10-missing-student-http-404.png` |
| API-07 equipment lookup build | API with equipment-by-ID route built successfully. | `api07-03-build-succeeded.png` |
| API-07 missing equipment | GET unknown equipment ID returned `not_found` JSON and HTTP 404. | `api07-04-equipment-not-found-body.png`, `api07-05-equipment-not-found-http-404.png` |
| API-07 existing equipment | GET equipment ID 1 returned HTTP 200 with code, name, description, and availability. | `api07-07-equipment-by-id-http-200.png` |
| API-08 registration build | API with equipment registration route built successfully. | `api08-04-build-succeeded.png` |
| API-08 create equipment | POST created equipment ID 2 with HTTP 201, Location `/api/v1/equipment/2`, and complete JSON body. | `api08-05-register-equipment-http-201.png` |
| API-08 duplicate rejection | Repeating POST returned HTTP 409 with `duplicate_equipment_code`. GET equipment still showed two rows. | `api08-06-duplicate-equipment-http-409.png`, `api08-07-two-equipment-after-duplicate.png` |
| API-08 independent database readback | PostgreSQL showed exactly two available equipment rows after duplicate rejection. | `api08-08-postgresql-two-equipment-after-duplicate.png` |
| API-09 update build | API with equipment update route built successfully. | `api09-03-build-succeeded.png` |
| API-09 update and GET readback | PUT equipment ID 1 returned HTTP 200 with updated name and description; GET by ID showed the same fields with availability still true. | `api09-04-update-equipment-http-200.png`, `api09-05-update-equipment-get-readback.png` |
| API-09 independent update readback | PostgreSQL showed the updated name and description with availability unchanged. | `api09-06-postgresql-update-readback.png` |
| API-09 clear description | PUT equipment ID 1 with `description: null` returned HTTP 200; GET and PostgreSQL showed the description is null while availability stayed true. | `api09-07-clear-description-http-200.png`, `api09-08-clear-description-get-readback.png`, `api09-09-postgresql-description-null.png` |
| API-09 missing equipment | PUT unknown equipment ID returned HTTP 404 with `not_found`. | `api09-10-missing-equipment-http-404.png` |
| API-09 blank name | PUT equipment ID 1 with blank name returned HTTP 400 and `invalid_equipment`. | `api09-11-blank-name-http-400.png` |
| API-09 duplicate code | PUT equipment ID 1 using ID 2's code returned HTTP 409 and `duplicate_equipment_code`. PostgreSQL still had the original codes and exactly two rows. | `api09-12-duplicate-code-http-409.png`, `api09-13-postgresql-unchanged-after-rejections.png` |
| API-09 protected field correction | PUT equipment ID 1 with `isAvailable: false` returned HTTP 400 and `invalid_equipment`; a subsequent GET still showed `isAvailable: true`. Earlier route screenshots were removed because they preceded this required guard. | `api09-15-protected-field-http-400.png`, `api09-16-availability-unchanged-after-400.png` |
| API-10 single equipment borrow | POST `/api/v1/borrowings` returned HTTP 201 with Location `/api/v1/borrowings/1` and the active borrowing details. | `api10-02-borrow-equipment-http-201.png` |
| API-10 independent database readback | PostgreSQL showed borrowing ID 1 for student 1 and equipment 1, borrowed on 2026-11-15 with expected return 2026-11-20 and Active status. | `api10-03-postgresql-borrowing-readback.png` |
| API-10 borrowing availability readback | PostgreSQL showed equipment ID 1 marked unavailable after the borrowing. | `api10-04-postgresql-equipment-unavailable.png` |
| API-10 already borrowed item | Repeating the borrowing request for equipment ID 1 returned HTTP 409 and `borrowing_conflict`. | `api10-05-unavailable-equipment-http-409.png` |
| API-10 invalid date | Past expected return date returned HTTP 400 and `invalid_borrowing`. | `api10-06-past-date-http-400.png` |
| API-10 missing student | Borrowing with unknown student ID returned HTTP 404 and `not_found`. | `api10-07-missing-student-http-404.png` |
| API-10 restricted student | Student 1 was restricted; borrowing equipment ID 2 returned HTTP 409 and `borrowing_conflict`. Student eligibility was restored afterward. | `api10-08-restrict-student-for-borrow-test.png`, `api10-09-restricted-student-http-409.png`, `api10-10-restore-student-eligibility.png` |
| API-10 build | API including single equipment borrowing built successfully. | `api10-11-build-succeeded.png` |
| API-11 route, build, and successful checkout | `POST /api/v1/checkout-requests` is implemented; the API build succeeded, and the multi-item checkout returned HTTP 201 with two Active borrowings. PostgreSQL confirmed both borrowing rows and both equipment items unavailable. Over-limit rejection returned HTTP 409; unavailable equipment returned HTTP 409 and stated nothing was saved. PostgreSQL confirmed equipment 4 remains available and has zero borrowings for student 2 after rejection. Duplicate equipment IDs returned HTTP 400 with `duplicate_equipment_id`; a single-item checkout and missing return date each returned HTTP 400 with `invalid_checkout`; an unknown equipment ID returned HTTP 404 and stated nothing was saved. | `api11-01-build-succeeded.png` through `api11-13-missing-equipment-http-404.png` |

| API-12 return endpoint | API build succeeded; returning borrowing 2 returned HTTP 200, PostgreSQL confirmed it is Returned and equipment 2 is available, repeat return returned HTTP 409, and unknown borrowing returned HTTP 404. | pi12-01-build-succeeded.png, pi12-02-return-http-200.png, pi12-03-postgresql-return-readback.png, pi12-04-repeat-return-http-409.png, pi12-05-missing-borrowing-http-404.png |