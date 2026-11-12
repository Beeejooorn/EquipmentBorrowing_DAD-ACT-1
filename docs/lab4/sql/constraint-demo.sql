-- Lab 4 / Appendix C A17: invalid writes sent straight to PostgreSQL.
-- Run ONLY on a disposable database that was just created with the migrations, e.g.:
--   1. point ConnectionStrings__EquipmentBorrowing at a NEW database name, e.g. equipmentborrowing_clean
--   2. dotnet ef database update --project src/EquipmentBorrowing.Infrastructure   (EF creates the database)
--   3. psql -U postgres -d equipmentborrowing_clean -f constraint-demo.sql
--   4. psql -U postgres -d equipmentborrowing_clean -f readback.sql   (a new connection = fresh read)
-- Every 'Test' below must end with ERROR. Test 9 shows that the first write inside the
-- transaction is accepted, the second fails, and the whole transaction is rolled back.

\echo '--- Setup: one student, two items, one active borrowing (Laptop 01)'
INSERT INTO "Students" ("StudentNumber", "Name", "ContactInfo", "IsAllowedToBorrow") VALUES ('2023-0001', 'Ana Reyes', NULL, true);
INSERT INTO "Equipment" ("EquipmentCode", "Name", "Description", "IsAvailable") VALUES ('EQ-LAP-01', 'Laptop 01', NULL, false), ('EQ-LAP-02', 'Laptop 02', NULL, true);
INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "Status") VALUES (1, 1, CURRENT_DATE, CURRENT_DATE + 7, 'Active');

\echo ''
\echo '--- Test 1 (BR-14): borrowing for a student that does not exist -> must be REJECTED (foreign key)'
INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "Status") VALUES (999, 2, CURRENT_DATE, CURRENT_DATE + 7, 'Active');

\echo ''
\echo '--- Test 2 (BR-01): same student number with extra spaces -> must be REJECTED (unique)'
INSERT INTO "Students" ("StudentNumber", "Name", "IsAllowedToBorrow") VALUES ('  2023-0001 ', 'Copy of Ana', true);

\echo ''
\echo '--- Test 3 (BR-05): same equipment code in lower case with spaces -> must be REJECTED (unique)'
INSERT INTO "Equipment" ("EquipmentCode", "Name", "IsAvailable") VALUES (' eq-lap-02 ', 'Copy of Laptop 02', true);

\echo ''
\echo '--- Test 4 (BR-10): a second ACTIVE borrowing for Laptop 01 -> must be REJECTED (partial unique index)'
INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "Status") VALUES (1, 1, CURRENT_DATE, CURRENT_DATE + 3, 'Active');

\echo ''
\echo '--- Test 5 (BR-11): expected return date equal to checkout date -> must be REJECTED (check)'
INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "Status") VALUES (1, 2, CURRENT_DATE, CURRENT_DATE, 'Active');

\echo ''
\echo '--- Test 6 (BR-02/BR-06): blank name and missing code -> must be REJECTED (check / not null)'
INSERT INTO "Students" ("StudentNumber", "Name", "IsAllowedToBorrow") VALUES ('2023-0500', '   ', true);
INSERT INTO "Equipment" ("EquipmentCode", "Name", "IsAvailable") VALUES (NULL, 'No Code', true);

\echo ''
\echo '--- Test 7 (BR-13): status Returned without a return date -> must be REJECTED (check)'
INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "Status") VALUES (1, 2, CURRENT_DATE, CURRENT_DATE + 7, 'Returned');

\echo ''
\echo '--- Test 8 (BR-14): delete a student that has borrowing history -> must be REJECTED (ON DELETE RESTRICT)'
DELETE FROM "Students" WHERE "Id" = 1;

\echo ''
\echo '--- Test 9 (BR-15): one transaction, the FIRST write is accepted, the SECOND write fails'
BEGIN;
INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "Status") VALUES (1, 2, CURRENT_DATE, CURRENT_DATE + 7, 'Active');
UPDATE "Equipment" SET "IsAvailable" = false WHERE "Id" = 2;
\echo 'Inside the transaction (not committed yet): Laptop 02 is borrowed here'
SELECT "Id", "EquipmentId", "Status" FROM "Borrowings" ORDER BY "Id";
\echo 'Second write: another active borrowing for Laptop 01 -> fails'
INSERT INTO "Borrowings" ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "Status") VALUES (1, 1, CURRENT_DATE, CURRENT_DATE + 7, 'Active');
\echo 'COMMIT after an error: PostgreSQL answers ROLLBACK, so nothing of this transaction is kept'
COMMIT;
