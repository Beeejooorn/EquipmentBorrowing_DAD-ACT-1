-- Activity 5 acceptance dataset from Section 3 of the API and Acceptance Reference.
-- Run only against a disposable database whose name ends in _lab5_acceptance.
-- Dates use the server's local CURRENT_DATE as the reference date so the
-- expected return date for E1 is yesterday and the other due dates are future.

BEGIN;

DO $$
BEGIN
    IF current_database() !~ '_lab5_acceptance$' THEN
        RAISE EXCEPTION 'Refusing to reset %. Use a disposable *_lab5_acceptance database.', current_database();
    END IF;
END $$;

TRUNCATE TABLE "Borrowings", "Equipment", "Students" RESTART IDENTITY CASCADE;

INSERT INTO "Students" ("StudentNumber", "Name", "ContactInfo", "IsAllowedToBorrow") VALUES
    ('ST-100', 'Ada Cruz', NULL, true),
    ('ST-200', 'Ben Diaz', NULL, false),
    ('ST-300', 'Cara Lim', NULL, true),
    ('ST-400', 'Dani Ong', NULL, true);

INSERT INTO "Equipment" ("EquipmentCode", "Name", "Description", "IsAvailable") VALUES
    ('EQ-001', 'Camera', NULL, false),
    ('EQ-002', 'Laptop', NULL, true),
    ('EQ-003', 'Projector', NULL, true),
    ('EQ-004', 'Tablet', NULL, false),
    ('EQ-005', 'Microphone', NULL, false),
    ('EQ-006', 'Tripod', NULL, true),
    ('EQ-007', 'Speaker', NULL, true);

INSERT INTO "Borrowings"
    ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "Status", "ReturnedAt")
SELECT s."Id", e."Id", CURRENT_DATE - 9, CURRENT_DATE - 1, 'Active', NULL
FROM "Students" s CROSS JOIN "Equipment" e
WHERE s."StudentNumber" = 'ST-100' AND e."EquipmentCode" = 'EQ-001';

INSERT INTO "Borrowings"
    ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "Status", "ReturnedAt")
SELECT s."Id", e."Id", CURRENT_DATE - 8, CURRENT_DATE + 2, 'Returned', CURRENT_DATE - 2
FROM "Students" s CROSS JOIN "Equipment" e
WHERE s."StudentNumber" = 'ST-100' AND e."EquipmentCode" = 'EQ-003';

INSERT INTO "Borrowings"
    ("StudentId", "EquipmentId", "DateBorrowed", "ExpectedReturnDate", "Status", "ReturnedAt")
SELECT s."Id", e."Id", CURRENT_DATE - 7, CURRENT_DATE + 2, 'Active', NULL
FROM "Students" s CROSS JOIN "Equipment" e
WHERE s."StudentNumber" = 'ST-300' AND e."EquipmentCode" IN ('EQ-004', 'EQ-005');

COMMIT;

SELECT CURRENT_DATE AS "LocalTestDate", (SELECT count(*) FROM "Students") AS "Students",
       (SELECT count(*) FROM "Equipment") AS "Equipment",
       (SELECT count(*) FROM "Borrowings" WHERE "Status" = 'Active') AS "ActiveBorrowings",
       (SELECT count(*) FROM "Borrowings" WHERE "Status" = 'Returned') AS "ReturnedBorrowings";
