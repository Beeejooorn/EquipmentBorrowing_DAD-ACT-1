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
