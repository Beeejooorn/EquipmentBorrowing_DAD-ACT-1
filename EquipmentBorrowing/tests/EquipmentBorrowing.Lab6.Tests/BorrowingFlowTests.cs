using System.Text.Json;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Lab6.Tests.Support;

namespace EquipmentBorrowing.Lab6.Tests;

// TC6-U03 Single borrowing (UC-03, API-10, API-13)
// TC6-U04 Active borrowings (UC-04, API-14)
// TC6-U05 Return and history (UC-05, API-12, API-13)
// TC6-U06 Atomic multi-item success (UC-06, API-11)
public class BorrowingFlowTests : ApiTestBase
{
    // ---------------- TC6-U03 ----------------

    [Fact]
    public async Task TC6_U03_Single_borrowing_creates_an_active_record_and_makes_the_item_unavailable()
    {
        int s4 = await StudentIdAsync("ST-400");
        int e2 = await EquipmentIdAsync("EQ-002");

        // 1. POST S4 / E2 due D+2.
        ApiResponse created = await PostAsync("/api/v1/borrowings", new
        {
            studentId = s4,
            equipmentId = e2,
            expectedReturnDate = Day(2)
        });

        Assert.Equal(201, created.Status);
        JsonElement body = created.Json();
        int borrowingId = body.GetProperty("id").GetInt32();

        Assert.True(borrowingId > 0);
        Assert.Equal(s4, body.GetProperty("studentId").GetInt32());
        Assert.Equal(e2, body.GetProperty("equipmentId").GetInt32());
        Assert.Equal(Day(0), body.GetProperty("borrowedDate").GetString());
        Assert.Equal(Day(2), body.GetProperty("expectedReturnDate").GetString());
        Assert.Equal("Active", body.GetProperty("status").GetString());

        // 2. The Location header points to the new record.
        Assert.Equal("/api/v1/borrowings/" + borrowingId, created.Location);

        // 3. GET the Location: 200 with the active record.
        ApiResponse fetched = await GetAsync(created.Location!);

        Assert.Equal(200, fetched.Status);
        JsonElement record = fetched.Json();
        Assert.Equal(borrowingId, record.GetProperty("id").GetInt32());
        Assert.Equal("Active", record.GetProperty("status").GetString());
        Assert.Equal("ST-400", record.GetProperty("studentNumber").GetString());
        Assert.Equal("EQ-002", record.GetProperty("equipmentCode").GetString());
        Assert.Equal(JsonValueKind.Null, record.GetProperty("returnedDate").ValueKind);

        // 4. Fresh PostgreSQL connection: ONE S4/E2 record and E2 is now unavailable.
        List<Borrowing> rows = LabDatabase.BorrowingsOfStudent(s4);
        Assert.Single(rows);
        Assert.Equal(borrowingId, rows[0].Id);
        Assert.Equal(e2, rows[0].EquipmentId);
        Assert.Equal(BorrowingStatus.Active, rows[0].Status);
        Assert.Equal(D, rows[0].DateBorrowed);
        Assert.Equal(D.AddDays(2), rows[0].ExpectedReturnDate);

        Assert.False(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);
        Assert.Equal(5, LabDatabase.CountBorrowings());
    }

    // ---------------- TC6-U04 ----------------

    [Fact]
    public async Task TC6_U04_Active_borrowings_list_E1_E4_E5_with_the_correct_students_and_exclude_returned_E3()
    {
        string before = LabDatabase.Snapshot();

        ApiResponse response = await GetAsync("/api/v1/borrowings?status=Active");

        Assert.Equal(200, response.Status);
        JsonElement list = response.Json();
        Assert.Equal(3, list.GetArrayLength());

        // Sorted by expected return date, then by borrowing Id: E1 (D-1), then E4 and E5 (both D+2).
        Assert.Equal(new List<string> { "EQ-001", "EQ-004", "EQ-005" }, BorrowedCodesOf(list));

        // Each record has the right borrowing Id (1, 3, 4), student and equipment details.
        Assert.Equal(1, list[0].GetProperty("id").GetInt32());
        Assert.Equal("ST-100", list[0].GetProperty("studentNumber").GetString());
        Assert.Equal("Ada", list[0].GetProperty("studentName").GetString());
        Assert.Equal("Camera", list[0].GetProperty("equipmentName").GetString());

        Assert.Equal(3, list[1].GetProperty("id").GetInt32());
        Assert.Equal("ST-300", list[1].GetProperty("studentNumber").GetString());
        Assert.Equal("Cara", list[1].GetProperty("studentName").GetString());
        Assert.Equal("Tablet", list[1].GetProperty("equipmentName").GetString());

        Assert.Equal(4, list[2].GetProperty("id").GetInt32());
        Assert.Equal("ST-300", list[2].GetProperty("studentNumber").GetString());
        Assert.Equal("Microphone", list[2].GetProperty("equipmentName").GetString());

        foreach (JsonElement record in list.EnumerateArray())
        {
            Assert.Equal("Active", record.GetProperty("status").GetString());
        }

        // The returned item E3 is NOT in the active list.
        Assert.DoesNotContain("EQ-003", BorrowedCodesOf(list));

        Assert.Equal(before, LabDatabase.Snapshot());
    }

    // ---------------- TC6-U05 ----------------

    [Fact]
    public async Task TC6_U05_Return_changes_the_record_to_Returned_keeps_its_identity_and_a_repeat_changes_nothing()
    {
        int borrowingId = 1; // S1 / E1 from the fixture
        int s1 = await StudentIdAsync("ST-100");
        int e1 = await EquipmentIdAsync("EQ-001");

        // 1. Return S1/E1: 200 and the record is Returned with date D.
        ApiResponse returned = await PostAsync("/api/v1/borrowings/" + borrowingId + "/return", new { });

        Assert.Equal(200, returned.Status);
        JsonElement body = returned.Json();
        Assert.Equal(borrowingId, body.GetProperty("id").GetInt32());
        Assert.Equal(s1, body.GetProperty("studentId").GetInt32());
        Assert.Equal(e1, body.GetProperty("equipmentId").GetInt32());
        Assert.Equal("Returned", body.GetProperty("status").GetString());
        Assert.Equal(Day(0), body.GetProperty("returnedDate").GetString());
        Assert.Equal(Day(-9), body.GetProperty("borrowedDate").GetString()); // original checkout date kept

        // 2. GET the record: still the same record, now Returned.
        ApiResponse fetched = await GetAsync("/api/v1/borrowings/" + borrowingId);
        Assert.Equal(200, fetched.Status);
        Assert.Equal("Returned", fetched.Json().GetProperty("status").GetString());

        // Fresh database state after the first return.
        Borrowing afterFirst = LabDatabase.FindBorrowing(borrowingId)!;
        Assert.Equal(BorrowingStatus.Returned, afterFirst.Status);
        Assert.Equal(D, afterFirst.ReturnedAt);
        Assert.True(LabDatabase.FindEquipment("EQ-001")!.IsAvailable);
        string snapshotAfterFirstReturn = LabDatabase.Snapshot();

        // 3. Return it AGAIN: 409 and nothing changes.
        ApiResponse repeat = await PostAsync("/api/v1/borrowings/" + borrowingId + "/return", new { });

        Assert.Equal(409, repeat.Status);
        Assert.Equal("already_returned", repeat.Json().GetProperty("code").GetString());
        Assert.Equal(snapshotAfterFirstReturn, LabDatabase.Snapshot());

        // The record was never deleted: still 4 rows, same Id, same original student and item.
        Assert.Equal(4, LabDatabase.CountBorrowings());
        Borrowing finalRow = LabDatabase.FindBorrowing(borrowingId)!;
        Assert.Equal(s1, finalRow.StudentId);
        Assert.Equal(e1, finalRow.EquipmentId);
    }

    // ---------------- TC6-U06 ----------------

    [Fact]
    public async Task TC6_U06_Multi_item_checkout_creates_two_active_records_and_makes_both_items_unavailable()
    {
        int s4 = await StudentIdAsync("ST-400");
        int e2 = await EquipmentIdAsync("EQ-002");
        int e6 = await EquipmentIdAsync("EQ-006");

        ApiResponse response = await PostAsync("/api/v1/checkout-requests", new
        {
            studentId = s4,
            equipmentIds = new int[] { e2, e6 },
            expectedReturnDate = Day(2)
        });

        Assert.Equal(201, response.Status);
        JsonElement borrowings = response.Json().GetProperty("borrowings");
        Assert.Equal(2, borrowings.GetArrayLength());

        // Both records: own Id, the right student and item (the "references"), Active, correct dates.
        int firstId = borrowings[0].GetProperty("id").GetInt32();
        int secondId = borrowings[1].GetProperty("id").GetInt32();
        Assert.NotEqual(firstId, secondId);

        Assert.Equal(e2, borrowings[0].GetProperty("equipmentId").GetInt32());
        Assert.Equal(e6, borrowings[1].GetProperty("equipmentId").GetInt32());

        foreach (JsonElement record in borrowings.EnumerateArray())
        {
            Assert.Equal(s4, record.GetProperty("studentId").GetInt32());
            Assert.Equal("Active", record.GetProperty("status").GetString());
            Assert.Equal(Day(0), record.GetProperty("borrowedDate").GetString());
            Assert.Equal(Day(2), record.GetProperty("expectedReturnDate").GetString());
        }

        // Fresh PostgreSQL read: two active records with those Ids, both items unavailable.
        List<Borrowing> rows = LabDatabase.BorrowingsOfStudent(s4);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Id == firstId && r.EquipmentId == e2 && r.Status == BorrowingStatus.Active);
        Assert.Contains(rows, r => r.Id == secondId && r.EquipmentId == e6 && r.Status == BorrowingStatus.Active);

        Assert.False(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);
        Assert.False(LabDatabase.FindEquipment("EQ-006")!.IsAvailable);
        Assert.Equal(6, LabDatabase.CountBorrowings());
    }
}
