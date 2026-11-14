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
