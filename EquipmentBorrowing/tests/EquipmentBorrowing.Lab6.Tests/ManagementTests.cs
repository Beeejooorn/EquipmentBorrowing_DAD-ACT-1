using System.Text.Json;
using EquipmentBorrowing.Lab6.Tests.Support;

namespace EquipmentBorrowing.Lab6.Tests;

// TC6-U12 Register student   (UC-12, API-03, API-04)
// TC6-U13 Update student     (UC-13, API-02, API-03, API-05, API-15)
// TC6-U14 Eligibility        (UC-14, API-06, API-10, API-12)
// TC6-U15 Register equipment (UC-15, API-07, API-08)
// TC6-U16 Update equipment   (UC-16, API-07, API-09, API-16)
public class ManagementTests : ApiTestBase
{
    // ---------------- TC6-U12 ----------------

    [Fact]
    public async Task TC6_U12_Register_student_ST500_creates_an_eligible_student_with_Location()
    {
        ApiResponse created = await PostAsync("/api/v1/students", new
        {
            studentNumber = "ST-500",
            fullName = "Eli Reyes",
            contact = "eli@example.test"
        });

        Assert.Equal(201, created.Status);
        JsonElement body = created.Json();
        int newId = body.GetProperty("id").GetInt32();

        Assert.True(newId > 0);
        Assert.Equal("ST-500", body.GetProperty("studentNumber").GetString());
        Assert.Equal("Eli Reyes", body.GetProperty("fullName").GetString());
        Assert.Equal("eli@example.test", body.GetProperty("contact").GetString());
        Assert.True(body.GetProperty("isEligible").GetBoolean());
        Assert.Equal("/api/v1/students/" + newId, created.Location);

        // Follow the Location header.
        ApiResponse followed = await GetAsync(created.Location!);
        Assert.Equal(200, followed.Status);
        Assert.Equal(newId, followed.Json().GetProperty("id").GetInt32());
        Assert.Equal("ST-500", followed.Json().GetProperty("studentNumber").GetString());
        Assert.True(followed.Json().GetProperty("isEligible").GetBoolean());

        // Fresh database check: exactly one new durable student (4 fixtures + 1).
        Assert.Equal(5, LabDatabase.CountStudents());
        var saved = LabDatabase.FindStudent("ST-500");
        Assert.NotNull(saved);
        Assert.Equal(newId, saved!.Id);
        Assert.Equal("Eli Reyes", saved.Name);
        Assert.Equal("eli@example.test", saved.ContactInfo);
        Assert.True(saved.IsAllowedToBorrow);
    }

    // ---------------- TC6-U13 ----------------

    [Fact]
    public async Task TC6_U13_Update_student_S1_keeps_the_same_id_and_the_same_borrowing_history()
    {
        int s1 = await StudentIdAsync("ST-100");

        // Step 1: API-02 search. A matching search returns S1, a non-matching one returns [].
        ApiResponse match = await GetAsync("/api/v1/students?search=ST-100");
        Assert.Equal(200, match.Status);
        Assert.Equal(1, match.Json().GetArrayLength());
        Assert.Equal(s1, match.Json()[0].GetProperty("id").GetInt32());

        ApiResponse noMatch = await GetAsync("/api/v1/students?search=zzz-nobody");
        Assert.Equal(200, noMatch.Status);
        Assert.Equal(0, noMatch.Json().GetArrayLength());

        // Remember S1's borrowing ids before the edit.
        List<int> idsBefore = LabDatabase.BorrowingsOfStudent(s1).Select(b => b.Id).OrderBy(x => x).ToList();
        Assert.Equal(new List<int> { 1, 2 }, idsBefore);

        // Step 2: GET S1, PUT a new name and contact, then read the history.
        ApiResponse before = await GetAsync("/api/v1/students/" + s1);
        Assert.Equal("Ada", before.Json().GetProperty("fullName").GetString());

        ApiResponse updated = await PutAsync("/api/v1/students/" + s1, new
        {
            studentNumber = "ST-100",
            fullName = "Ada Lovelace",
            contact = "ada.lovelace@example.test"
        });

        Assert.Equal(200, updated.Status);
        Assert.Equal(s1, updated.Json().GetProperty("id").GetInt32());
        Assert.Equal("Ada Lovelace", updated.Json().GetProperty("fullName").GetString());
        Assert.Equal("ada.lovelace@example.test", updated.Json().GetProperty("contact").GetString());
        Assert.True(updated.Json().GetProperty("isEligible").GetBoolean());

        ApiResponse history = await GetAsync("/api/v1/students/" + s1 + "/borrowings");
        Assert.Equal(200, history.Status);
        Assert.Equal(2, history.Json().GetArrayLength());
        foreach (JsonElement record in history.Json().EnumerateArray())
        {
            Assert.Equal(s1, record.GetProperty("studentId").GetInt32());
            Assert.Equal("Ada Lovelace", record.GetProperty("studentName").GetString());
        }

        // Step 3: fresh connection. Same id, new fields, same borrowing relationships.
        var saved = LabDatabase.FindStudent("ST-100");
        Assert.NotNull(saved);
        Assert.Equal(s1, saved!.Id);
        Assert.Equal("Ada Lovelace", saved.Name);
        Assert.Equal("ada.lovelace@example.test", saved.ContactInfo);

        List<int> idsAfter = LabDatabase.BorrowingsOfStudent(s1).Select(b => b.Id).OrderBy(x => x).ToList();
        Assert.Equal(idsBefore, idsAfter);
        Assert.Equal(4, LabDatabase.CountBorrowings());
        Assert.Equal(1, LabDatabase.CountActiveBorrowings(s1));
        Assert.Equal(4, LabDatabase.CountStudents());
    }

    // ---------------- TC6-U14 ----------------

    [Fact]
    public async Task TC6_U14_Restrict_S1_blocks_a_new_loan_but_return_still_works_and_permit_allows_issue_again()
    {
        int s1 = await StudentIdAsync("ST-100");
        int e2 = await EquipmentIdAsync("EQ-002");

        // 1. Restrict S1: 200 and isEligible=false.
        ApiResponse restricted = await PatchAsync("/api/v1/students/" + s1 + "/eligibility", new { isEligible = false });
        Assert.Equal(200, restricted.Status);
        Assert.False(restricted.Json().GetProperty("isEligible").GetBoolean());
        Assert.False(LabDatabase.FindStudent("ST-100")!.IsAllowedToBorrow);

        // The original loan (borrowing 1, S1/E1) survives the restriction.
        Assert.Equal(1, LabDatabase.CountActiveBorrowings(s1));

        // 2. A new issue of E2 to the restricted student: 409 and nothing is saved.
        string beforeBlocked = LabDatabase.Snapshot();
        ApiResponse blocked = await PostAsync("/api/v1/borrowings", new
        {
            studentId = s1,
            equipmentId = e2,
            expectedReturnDate = Day(2)
        });
        Assert.Equal(409, blocked.Status);
        Assert.Equal("borrowing_conflict", blocked.Json().GetProperty("code").GetString());
        AssertSafeErrorBody(blocked);
        Assert.Equal(beforeBlocked, LabDatabase.Snapshot());
        Assert.True(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);

        // 3. Return E1 (borrowing 1) while restricted: still allowed.
        ApiResponse returned = await PostAsync("/api/v1/borrowings/1/return", new { });
        Assert.Equal(200, returned.Status);
        Assert.Equal("Returned", returned.Json().GetProperty("status").GetString());
        Assert.Equal(0, LabDatabase.CountActiveBorrowings(s1));
        Assert.True(LabDatabase.FindEquipment("EQ-001")!.IsAvailable);

        // The returned record stays in the history (both of S1's records still exist).
        ApiResponse history = await GetAsync("/api/v1/students/" + s1 + "/borrowings");
        Assert.Equal(2, history.Json().GetArrayLength());

        // 4. Permit S1 again: 200 and isEligible=true.
        ApiResponse permitted = await PatchAsync("/api/v1/students/" + s1 + "/eligibility", new { isEligible = true });
        Assert.Equal(200, permitted.Status);
        Assert.True(permitted.Json().GetProperty("isEligible").GetBoolean());

        // 5. Issue E2 to S1: now 201.
        ApiResponse issued = await PostAsync("/api/v1/borrowings", new
        {
            studentId = s1,
            equipmentId = e2,
            expectedReturnDate = Day(2)
        });
        Assert.Equal(201, issued.Status);
        Assert.Equal("Active", issued.Json().GetProperty("status").GetString());

        Assert.Equal(1, LabDatabase.CountActiveBorrowings(s1));
        Assert.False(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);
        Assert.Equal(5, LabDatabase.CountBorrowings());
    }

    // ---------------- TC6-U15 ----------------

    [Fact]
    public async Task TC6_U15_Register_equipment_EQ008_is_available_and_a_duplicate_EQ001_is_409()
    {
        ApiResponse created = await PostAsync("/api/v1/equipment", new
        {
            equipmentCode = "EQ-008",
            name = "Monitor",
            description = "24 inch monitor"
        });

        Assert.Equal(201, created.Status);
        JsonElement body = created.Json();
        int newId = body.GetProperty("id").GetInt32();

        Assert.True(newId > 0);
        Assert.Equal("EQ-008", body.GetProperty("equipmentCode").GetString());
        Assert.Equal("Monitor", body.GetProperty("name").GetString());
        Assert.Equal("24 inch monitor", body.GetProperty("description").GetString());
        Assert.True(body.GetProperty("isAvailable").GetBoolean());
        Assert.Equal("/api/v1/equipment/" + newId, created.Location);

        // Follow Location.
        ApiResponse followed = await GetAsync(created.Location!);
        Assert.Equal(200, followed.Status);
        Assert.Equal(newId, followed.Json().GetProperty("id").GetInt32());
        Assert.True(followed.Json().GetProperty("isAvailable").GetBoolean());

        // Duplicate of EQ-001: 409 and nothing new saved.
        string beforeDuplicate = LabDatabase.Snapshot();
        ApiResponse duplicate = await PostAsync("/api/v1/equipment", new
        {
            equipmentCode = "EQ-001",
            name = "Another camera",
            description = "duplicate attempt"
        });
        Assert.Equal(409, duplicate.Status);
        Assert.Equal("duplicate_equipment_code", duplicate.Json().GetProperty("code").GetString());
        AssertSafeErrorBody(duplicate);
        Assert.Equal(beforeDuplicate, LabDatabase.Snapshot());

        // Fresh database check: 7 fixtures + EQ-008 only, EQ-008 is available.
        Assert.Equal(8, LabDatabase.CountEquipment());
        var saved = LabDatabase.FindEquipment("EQ-008");
        Assert.NotNull(saved);
        Assert.Equal(newId, saved!.Id);
        Assert.True(saved.IsAvailable);
    }

    // ---------------- TC6-U16 ----------------

    [Fact]
    public async Task TC6_U16_Update_equipment_E1_changes_the_description_but_not_availability_or_history()
    {
        int e1 = await EquipmentIdAsync("EQ-001");

        List<int> idsBefore = LabDatabase.BorrowingsOfEquipment(e1).Select(b => b.Id).OrderBy(x => x).ToList();
        Assert.Equal(new List<int> { 1 }, idsBefore);

        // 1. Edit the descriptive field only (code and name stay the same).
        ApiResponse updated = await PutAsync("/api/v1/equipment/" + e1, new
        {
            equipmentCode = "EQ-001",
            name = "Camera",
            description = "Canon camera with new lens"
        });

        Assert.Equal(200, updated.Status);
        JsonElement body = updated.Json();
        Assert.Equal(e1, body.GetProperty("id").GetInt32());
        Assert.Equal("EQ-001", body.GetProperty("equipmentCode").GetString());
        Assert.Equal("Camera", body.GetProperty("name").GetString());
        Assert.Equal("Canon camera with new lens", body.GetProperty("description").GetString());
        Assert.False(body.GetProperty("isAvailable").GetBoolean());

        // Retrieve the history: the original ACTIVE record is still there.
        ApiResponse history = await GetAsync("/api/v1/equipment/" + e1 + "/borrowings");
        Assert.Equal(200, history.Status);
        Assert.Equal(1, history.Json().GetArrayLength());
        Assert.Equal(1, history.Json()[0].GetProperty("id").GetInt32());
        Assert.Equal("Active", history.Json()[0].GetProperty("status").GetString());
        Assert.Equal("ST-100", history.Json()[0].GetProperty("studentNumber").GetString());

        // 2. Fresh database: same id, new description, still unavailable, same borrowing.
        var saved = LabDatabase.FindEquipment("EQ-001");
        Assert.NotNull(saved);
        Assert.Equal(e1, saved!.Id);
        Assert.Equal("Canon camera with new lens", saved.Description);
        Assert.False(saved.IsAvailable);

        List<int> idsAfter = LabDatabase.BorrowingsOfEquipment(e1).Select(b => b.Id).OrderBy(x => x).ToList();
        Assert.Equal(idsBefore, idsAfter);
        Assert.Equal(7, LabDatabase.CountEquipment());
        Assert.Equal(4, LabDatabase.CountBorrowings());
    }
}
