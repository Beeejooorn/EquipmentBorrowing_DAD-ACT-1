using System.Text.Json;
using EquipmentBorrowing.Lab6.Tests.Support;

namespace EquipmentBorrowing.Lab6.Tests;

// TC6-E01 Normalized uniqueness
// TC6-E02 Limit boundaries
// TC6-E03 Protocol errors
// TC6-E06 Optional and protected fields
public class ValidationEdgeTests : ApiTestBase
{
    // An error body must never show internal details to the user.
    private static void AssertNoInternalDetails(ApiResponse response)
    {
        Assert.DoesNotContain("Exception", response.Body);
        Assert.DoesNotContain("StackTrace", response.Body);
        Assert.DoesNotContain("   at ", response.Body);
        Assert.DoesNotContain("Npgsql", response.Body);
        Assert.DoesNotContain("Host=", response.Body);
        Assert.DoesNotContain("Password", response.Body);
    }

    // ---------------- TC6-E01 ----------------

    [Fact]
    public async Task TC6_E01_Student_number_variants_are_duplicates_for_create_and_update()
    {
        string before = LabDatabase.Snapshot();
        int s3 = await StudentIdAsync("ST-300");

        // Create: lower case, spaces around, and both together.
        string[] variants = { "st-100", "  ST-100  ", " st-100 " };

        foreach (string variant in variants)
        {
            ApiResponse response = await PostAsync("/api/v1/students", new
            {
                studentNumber = variant,
                fullName = "Copy Cat",
                contact = "copy@example.test"
            });

            Assert.Equal(409, response.Status);
            Assert.Equal("duplicate_student_number", response.Json().GetProperty("code").GetString());
            AssertNoInternalDetails(response);
        }

        // Update: S3 tries to take S1's number in a different style.
        ApiResponse update = await PutAsync("/api/v1/students/" + s3, new
        {
            studentNumber = "st-100",
            fullName = "Cara",
            contact = "cara@example.test"
        });

        Assert.Equal(409, update.Status);
        Assert.Equal("duplicate_student_number", update.Json().GetProperty("code").GetString());

        // Fresh counts and rows: nothing changed.
        Assert.Equal(4, LabDatabase.CountStudents());
        Assert.Equal("ST-300", LabDatabase.FindStudent("ST-300")!.StudentNumber);
        Assert.Equal(before, LabDatabase.Snapshot());
    }

    [Fact]
    public async Task TC6_E01_Equipment_code_variants_are_duplicates_for_create_and_update()
    {
        string before = LabDatabase.Snapshot();
        int e3 = await EquipmentIdAsync("EQ-003");

        string[] variants = { "eq-001", "  EQ-001  ", " eq-001" };

        foreach (string variant in variants)
        {
            ApiResponse response = await PostAsync("/api/v1/equipment", new
            {
                equipmentCode = variant,
                name = "Copy camera",
                description = "duplicate attempt"
            });

            Assert.Equal(409, response.Status);
            Assert.Equal("duplicate_equipment_code", response.Json().GetProperty("code").GetString());
            AssertNoInternalDetails(response);
        }

        // Update: E3 tries to take E1's code.
        ApiResponse update = await PutAsync("/api/v1/equipment/" + e3, new
        {
            equipmentCode = "eq-001",
            name = "Projector",
            description = "Epson projector"
        });

        Assert.Equal(409, update.Status);
        Assert.Equal("duplicate_equipment_code", update.Json().GetProperty("code").GetString());

        Assert.Equal(7, LabDatabase.CountEquipment());
        Assert.Equal(before, LabDatabase.Snapshot());
    }

    [Fact]
    public void TC6_E01_PostgreSQL_itself_rejects_duplicate_student_numbers_and_equipment_codes()
    {
        // These statements skip the API completely and go straight to PostgreSQL.
        var duplicateStudent = LabDatabase.TryRawSql(
            "INSERT INTO \"Students\" (\"ContactInfo\", \"IsAllowedToBorrow\", \"Name\", \"StudentNumber\") " +
            "VALUES (NULL, true, 'Direct Copy', ' st-100 ')");

        Assert.NotNull(duplicateStudent);
        Assert.Equal(LabDatabase.UniqueViolation, duplicateStudent!.SqlState);

        var duplicateEquipment = LabDatabase.TryRawSql(
            "INSERT INTO \"Equipment\" (\"Description\", \"EquipmentCode\", \"IsAvailable\", \"Name\") " +
            "VALUES (NULL, ' eq-001 ', true, 'Direct Copy')");

        Assert.NotNull(duplicateEquipment);
        Assert.Equal(LabDatabase.UniqueViolation, duplicateEquipment!.SqlState);

        // Fresh counts: neither bypass inserted anything.
        Assert.Equal(4, LabDatabase.CountStudents());
        Assert.Equal(7, LabDatabase.CountEquipment());
    }

    // ---------------- TC6-E02 ----------------
    // The limit is three active borrowings per student (BR-09).

    [Fact]
    public async Task TC6_E02_S3_with_two_active_can_borrow_one_more_single_item_up_to_exactly_three()
    {
        int s3 = await StudentIdAsync("ST-300");
        int e2 = await EquipmentIdAsync("EQ-002");
        int e6 = await EquipmentIdAsync("EQ-006");

        Assert.Equal(2, LabDatabase.CountActiveBorrowings(s3));

        ApiResponse third = await PostAsync("/api/v1/borrowings", new
        {
            studentId = s3,
            equipmentId = e2,
            expectedReturnDate = Day(2)
        });

        Assert.Equal(201, third.Status);
        Assert.Equal(3, LabDatabase.CountActiveBorrowings(s3));
        Assert.False(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);

        // A fourth one is over the limit.
        string beforeFourth = LabDatabase.Snapshot();

        ApiResponse fourth = await PostAsync("/api/v1/borrowings", new
        {
            studentId = s3,
            equipmentId = e6,
            expectedReturnDate = Day(2)
        });

        Assert.Equal(409, fourth.Status);
        Assert.Equal("borrowing_conflict", fourth.Json().GetProperty("code").GetString());
        Assert.Equal(beforeFourth, LabDatabase.Snapshot());
        Assert.True(LabDatabase.FindEquipment("EQ-006")!.IsAvailable);
    }

    [Fact]
    public async Task TC6_E02_S1_with_one_active_can_check_out_two_items_to_reach_exactly_three()
    {
        int s1 = await StudentIdAsync("ST-100");
        int e2 = await EquipmentIdAsync("EQ-002");
        int e6 = await EquipmentIdAsync("EQ-006");

        Assert.Equal(1, LabDatabase.CountActiveBorrowings(s1));

        ApiResponse checkout = await PostAsync("/api/v1/checkout-requests", new
        {
            studentId = s1,
            equipmentIds = new int[] { e2, e6 },
            expectedReturnDate = Day(3)
        });

        Assert.Equal(201, checkout.Status);
        Assert.Equal(2, checkout.Json().GetProperty("borrowings").GetArrayLength());

        Assert.Equal(3, LabDatabase.CountActiveBorrowings(s1));
        Assert.False(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);
        Assert.False(LabDatabase.FindEquipment("EQ-006")!.IsAvailable);
        Assert.Equal(6, LabDatabase.CountBorrowings());
    }

    [Fact]
    public async Task TC6_E02_S3_with_two_active_cannot_check_out_two_more_and_nothing_is_partly_saved()
    {
        int s3 = await StudentIdAsync("ST-300");
        int e2 = await EquipmentIdAsync("EQ-002");
        int e6 = await EquipmentIdAsync("EQ-006");

        string before = LabDatabase.Snapshot();

        ApiResponse checkout = await PostAsync("/api/v1/checkout-requests", new
        {
            studentId = s3,
            equipmentIds = new int[] { e2, e6 },
            expectedReturnDate = Day(3)
        });

        Assert.Equal(409, checkout.Status);
        Assert.Equal("checkout_conflict", checkout.Json().GetProperty("code").GetString());
        AssertNoInternalDetails(checkout);

        // No partial writes: still 2 active, both items still available, same rows as before.
        Assert.Equal(2, LabDatabase.CountActiveBorrowings(s3));
        Assert.True(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);
        Assert.True(LabDatabase.FindEquipment("EQ-006")!.IsAvailable);
        Assert.Equal(4, LabDatabase.CountBorrowings());
        Assert.Equal(before, LabDatabase.Snapshot());
    }

    // ---------------- TC6-E03 ----------------

    [Fact]
    public async Task TC6_E03_Invalid_requests_are_400_and_change_nothing()
    {
        string before = LabDatabase.Snapshot();
        int s4 = await StudentIdAsync("ST-400");
        int e2 = await EquipmentIdAsync("EQ-002");
        int e6 = await EquipmentIdAsync("EQ-006");

        // Invalid JSON (not JSON at all).
        ApiResponse badJson = await PostRawAsync("/api/v1/students", "{ this is not json");
        Assert.Equal(400, badJson.Status);
        AssertNoInternalDetails(badJson);

        // Missing required student number, missing equipment name.
        ApiResponse noNumber = await PostAsync("/api/v1/students", new { studentNumber = "", fullName = "No Number" });
        Assert.Equal(400, noNumber.Status);
        Assert.Equal("invalid_student", noNumber.Json().GetProperty("code").GetString());

        ApiResponse noName = await PostAsync("/api/v1/equipment", new { equipmentCode = "EQ-900", name = "  " });
        Assert.Equal(400, noName.Status);
        Assert.Equal("invalid_equipment", noName.Json().GetProperty("code").GetString());

        // Too long: student number of 31 characters (the limit is 30).
        ApiResponse tooLong = await PostAsync("/api/v1/students", new
        {
            studentNumber = new string('9', 31),
            fullName = "Too Long"
        });
        Assert.Equal(400, tooLong.Status);
        AssertNoInternalDetails(tooLong);

        // Invalid dates: today, the past, and text that is not a date.
        ApiResponse today = await PostAsync("/api/v1/borrowings", new { studentId = s4, equipmentId = e2, expectedReturnDate = Day(0) });
        Assert.Equal(400, today.Status);
        Assert.Equal("invalid_borrowing", today.Json().GetProperty("code").GetString());

        ApiResponse past = await PostAsync("/api/v1/borrowings", new { studentId = s4, equipmentId = e2, expectedReturnDate = Day(-1) });
        Assert.Equal(400, past.Status);

        ApiResponse notADate = await PostRawAsync("/api/v1/borrowings",
            "{\"studentId\":" + s4 + ",\"equipmentId\":" + e2 + ",\"expectedReturnDate\":\"tomorrow\"}");
        Assert.Equal(400, notADate.Status);
        AssertNoInternalDetails(notADate);

        // Missing date.
        ApiResponse noDate = await PostAsync("/api/v1/borrowings", new { studentId = s4, equipmentId = e2 });
        Assert.Equal(400, noDate.Status);

        // Checkout with one item, and with the same item twice.
        ApiResponse oneItem = await PostAsync("/api/v1/checkout-requests", new
        {
            studentId = s4,
            equipmentIds = new int[] { e2 },
            expectedReturnDate = Day(2)
        });
        Assert.Equal(400, oneItem.Status);

        ApiResponse sameTwice = await PostAsync("/api/v1/checkout-requests", new
        {
            studentId = s4,
            equipmentIds = new int[] { e2, e2 },
            expectedReturnDate = Day(2)
        });
        Assert.Equal(400, sameTwice.Status);
        Assert.Equal("duplicate_equipment_id", sameTwice.Json().GetProperty("code").GetString());

        // Invalid eligibility value and invalid query value.
        ApiResponse noEligibility = await PatchAsync("/api/v1/students/" + s4 + "/eligibility", new { });
        Assert.Equal(400, noEligibility.Status);

        ApiResponse badQuery = await GetAsync("/api/v1/equipment?available=maybe");
        Assert.Equal(400, badQuery.Status);

        // None of the rejected writes changed the database.
        Assert.Equal(before, LabDatabase.Snapshot());
        Assert.True(LabDatabase.FindEquipment("EQ-006")!.IsAvailable);
        Assert.Equal(e6, LabDatabase.FindEquipment("EQ-006")!.Id);
    }

    [Fact]
    public async Task TC6_E03_Missing_resources_and_unknown_routes_are_404_and_wrong_methods_are_405()
    {
        string before = LabDatabase.Snapshot();
        int s4 = await StudentIdAsync("ST-400");
        int e2 = await EquipmentIdAsync("EQ-002");

        // 404: resource that does not exist.
        ApiResponse noStudent = await GetAsync("/api/v1/students/999999");
        Assert.Equal(404, noStudent.Status);
        Assert.Equal("not_found", noStudent.Json().GetProperty("code").GetString());

        ApiResponse borrowUnknownStudent = await PostAsync("/api/v1/borrowings", new
        {
            studentId = 999999,
            equipmentId = e2,
            expectedReturnDate = Day(2)
        });
        Assert.Equal(404, borrowUnknownStudent.Status);

        ApiResponse borrowUnknownItem = await PostAsync("/api/v1/borrowings", new
        {
            studentId = s4,
            equipmentId = 999999,
            expectedReturnDate = Day(2)
        });
        Assert.Equal(404, borrowUnknownItem.Status);

        ApiResponse returnUnknown = await PostAsync("/api/v1/borrowings/999999/return", new { });
        Assert.Equal(404, returnUnknown.Status);

        // 404: unknown route.
        ApiResponse unknownRoute = await GetAsync("/api/v1/nothing-here");
        Assert.Equal(404, unknownRoute.Status);
        AssertNoInternalDetails(unknownRoute);

        // 405: the route exists but not for this method.
        ApiResponse deleteStudents = await SendAsync(HttpMethod.Delete, "/api/v1/students");
        Assert.Equal(405, deleteStudents.Status);

        ApiResponse putCollection = await SendAsync(HttpMethod.Put, "/api/v1/equipment",
            JsonContentFor(new { equipmentCode = "EQ-001", name = "x" }));
        Assert.Equal(405, putCollection.Status);

        ApiResponse patchBorrowings = await SendAsync(HttpMethod.Patch, "/api/v1/borrowings",
            JsonContentFor(new { }));
        Assert.Equal(405, patchBorrowings.Status);

        Assert.Equal(before, LabDatabase.Snapshot());
    }

    [Fact]
    public async Task TC6_E03_Business_conflicts_are_409_with_a_safe_body_and_change_nothing()
    {
        int s1 = await StudentIdAsync("ST-100");
        int s2 = await StudentIdAsync("ST-200");
        int e1 = await EquipmentIdAsync("EQ-001");

        string before = LabDatabase.Snapshot();

        // S2 is restricted.
        ApiResponse restricted = await PostAsync("/api/v1/borrowings", new
        {
            studentId = s2,
            equipmentId = await EquipmentIdAsync("EQ-002"),
            expectedReturnDate = Day(2)
        });
        Assert.Equal(409, restricted.Status);
        Assert.Equal("borrowing_conflict", restricted.Json().GetProperty("code").GetString());
        AssertNoInternalDetails(restricted);

        // E1 is already borrowed.
        ApiResponse alreadyBorrowed = await PostAsync("/api/v1/borrowings", new
        {
            studentId = s1,
            equipmentId = e1,
            expectedReturnDate = Day(2)
        });
        Assert.Equal(409, alreadyBorrowed.Status);
        AssertNoInternalDetails(alreadyBorrowed);

        Assert.Equal(before, LabDatabase.Snapshot());
    }

    private static HttpContent JsonContentFor(object body)
    {
        return System.Net.Http.Json.JsonContent.Create(body);
    }

    // ---------------- TC6-E06 ----------------

    [Fact]
    public async Task TC6_E06_Optional_fields_can_be_cleared_to_null()
    {
        int s1 = await StudentIdAsync("ST-100");
        int e1 = await EquipmentIdAsync("EQ-001");

        // Student: clear the contact.
        ApiResponse student = await PutAsync("/api/v1/students/" + s1, new
        {
            studentNumber = "ST-100",
            fullName = "Ada",
            contact = (string?)null
        });
        Assert.Equal(200, student.Status);
        Assert.Equal(JsonValueKind.Null, student.Json().GetProperty("contact").ValueKind);
        Assert.Null(LabDatabase.FindStudent("ST-100")!.ContactInfo);

        // Equipment: clear the description (blank text also becomes null).
        ApiResponse equipment = await PutAsync("/api/v1/equipment/" + e1, new
        {
            equipmentCode = "EQ-001",
            name = "Camera",
            description = "   "
        });
        Assert.Equal(200, equipment.Status);
        Assert.Equal(JsonValueKind.Null, equipment.Json().GetProperty("description").ValueKind);
        Assert.Null(LabDatabase.FindEquipment("EQ-001")!.Description);

        // Clearing optional fields did not change availability or history.
        Assert.False(LabDatabase.FindEquipment("EQ-001")!.IsAvailable);
        Assert.Equal(4, LabDatabase.CountBorrowings());
    }

    [Fact]
    public async Task TC6_E06_Blank_required_duplicate_and_protected_edits_are_refused_and_leave_data_intact()
    {
        int s1 = await StudentIdAsync("ST-100");
        int e1 = await EquipmentIdAsync("EQ-001");

        string before = LabDatabase.Snapshot();

        // 400: blank required fields.
        ApiResponse blankName = await PutAsync("/api/v1/students/" + s1, new
        {
            studentNumber = "ST-100",
            fullName = "   ",
            contact = "ada@example.test"
        });
        Assert.Equal(400, blankName.Status);

        ApiResponse blankEquipmentName = await PutAsync("/api/v1/equipment/" + e1, new
        {
            equipmentCode = "EQ-001",
            name = "",
            description = "Canon camera"
        });
        Assert.Equal(400, blankEquipmentName.Status);

        // 409: S1 tries to take S2's number.
        ApiResponse duplicate = await PutAsync("/api/v1/students/" + s1, new
        {
            studentNumber = "ST-200",
            fullName = "Ada",
            contact = "ada@example.test"
        });
        Assert.Equal(409, duplicate.Status);

        // 400: isAvailable is protected and cannot be changed through an equipment edit.
        ApiResponse protectedField = await PutAsync("/api/v1/equipment/" + e1, new
        {
            equipmentCode = "EQ-001",
            name = "Camera",
            description = "Canon camera",
            isAvailable = true
        });
        Assert.Equal(400, protectedField.Status);
        Assert.Equal("invalid_equipment", protectedField.Json().GetProperty("code").GetString());

        // Fresh readback: E1 is still unavailable, its history is intact, nothing else changed.
        Assert.False(LabDatabase.FindEquipment("EQ-001")!.IsAvailable);
        Assert.Single(LabDatabase.BorrowingsOfEquipment(e1));
        Assert.Equal(before, LabDatabase.Snapshot());
    }

    [Fact]
    public async Task TC6_E06_A_student_edit_cannot_change_eligibility()
    {
        int s2 = await StudentIdAsync("ST-200");

        // S2 is restricted. An edit that also says isEligible=true must not lift the restriction.
        ApiResponse edit = await PutAsync("/api/v1/students/" + s2, new
        {
            studentNumber = "ST-200",
            fullName = "Ben",
            contact = "ben@example.test",
            isEligible = true
        });

        Assert.True(edit.Status == 200 || edit.Status == 400);
        Assert.False(LabDatabase.FindStudent("ST-200")!.IsAllowedToBorrow);
    }
}
