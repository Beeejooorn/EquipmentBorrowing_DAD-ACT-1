-- Fresh-connection readback after constraint-demo.sql.
-- Only the 4 setup rows may exist: 1 student, 2 items, 1 active borrowing (Laptop 01),
-- and Laptop 02 must still be available (Test 9 was rolled back).
SELECT "Id", "StudentNumber", "Name" FROM "Students" ORDER BY "Id";
SELECT "Id", "EquipmentCode", "Name", "IsAvailable" FROM "Equipment" ORDER BY "Id";
SELECT "Id", "StudentId", "EquipmentId", "Status", "ReturnedAt" FROM "Borrowings" ORDER BY "Id";
