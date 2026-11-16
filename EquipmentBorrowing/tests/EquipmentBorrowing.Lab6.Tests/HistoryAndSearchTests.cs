using System.Text.Json;
using EquipmentBorrowing.Lab6.Tests.Support;

namespace EquipmentBorrowing.Lab6.Tests;

// TC6-U07 Student history (UC-07, API-03, API-15)
// TC6-U08 Equipment history (UC-08, API-07, API-16)
// TC6-U09 Search and ordering (UC-09, API-14)
// All read-only: the database must be exactly the same after the requests.
public class HistoryAndSearchTests : ApiTestBase
{
    // Finds one record in a list of borrowings by its equipment code.
    private static JsonElement RecordFor(JsonElement list, string equipmentCode)
    {
        foreach (JsonElement record in list.EnumerateArray())
        {
            if (record.GetProperty("equipmentCode").GetString() == equipmentCode)
            {
                return record;
            }
        }

        throw new InvalidOperationException("No record for " + equipmentCode);
    }

    // ---------------- TC6-U07 ----------------

    [Fact]
    public async Task TC6_U07_Student_history_of_S1_has_active_E1_and_returned_E3_and_nothing_else()
    {
        string before = LabDatabase.Snapshot();
        int s1 = await StudentIdAsync("ST-100");

        ApiResponse response = await GetAsync("/api/v1/students/" + s1 + "/borrowings");

        Assert.Equal(200, response.Status);
        JsonElement list = response.Json();
        Assert.Equal(2, list.GetArrayLength());

        // Only S1's own records (no record of S3).
        foreach (JsonElement record in list.EnumerateArray())
        {
            Assert.Equal(s1, record.GetProperty("studentId").GetInt32());
            Assert.Equal("ST-100", record.GetProperty("studentNumber").GetString());
        }

        JsonElement e1 = RecordFor(list, "EQ-001");
        Assert.Equal("Active", e1.GetProperty("status").GetString());
        Assert.Equal(Day(-9), e1.GetProperty("borrowedDate").GetString());
        Assert.Equal(Day(-1), e1.GetProperty("expectedReturnDate").GetString());
        Assert.Equal(JsonValueKind.Null, e1.GetProperty("returnedDate").ValueKind);

        JsonElement e3 = RecordFor(list, "EQ-003");
        Assert.Equal("Returned", e3.GetProperty("status").GetString());
        Assert.Equal(Day(-8), e3.GetProperty("borrowedDate").GetString());
        Assert.Equal(Day(2), e3.GetProperty("expectedReturnDate").GetString());
        Assert.Equal(Day(-2), e3.GetProperty("returnedDate").GetString());

        Assert.Equal(before, LabDatabase.Snapshot());
    }

    [Fact]
    public async Task TC6_U07_Student_with_no_history_returns_an_empty_list_and_a_missing_student_returns_404()
    {
        int s4 = await StudentIdAsync("ST-400");

        // S4 exists but never borrowed anything: 200 and [].
        ApiResponse empty = await GetAsync("/api/v1/students/" + s4 + "/borrowings");
        Assert.Equal(200, empty.Status);
        Assert.Equal(JsonValueKind.Array, empty.Json().ValueKind);
        Assert.Equal(0, empty.Json().GetArrayLength());

        // A student that does not exist: 404 (different from an empty history).
        ApiResponse missing = await GetAsync("/api/v1/students/999999/borrowings");
        Assert.Equal(404, missing.Status);
        Assert.Equal("not_found", missing.Json().GetProperty("code").GetString());
    }

    [Fact]
    public async Task TC6_U07_Student_by_id_returns_the_student_or_404()
    {
        int s1 = await StudentIdAsync("ST-100");

        ApiResponse found = await GetAsync("/api/v1/students/" + s1);
        Assert.Equal(200, found.Status);
        JsonElement student = found.Json();
        Assert.Equal(s1, student.GetProperty("id").GetInt32());
        Assert.Equal("ST-100", student.GetProperty("studentNumber").GetString());
        Assert.Equal("Ada", student.GetProperty("fullName").GetString());
        Assert.Equal("ada@example.test", student.GetProperty("contact").GetString());
        Assert.True(student.GetProperty("isEligible").GetBoolean());

        ApiResponse missing = await GetAsync("/api/v1/students/999999");
        Assert.Equal(404, missing.Status);
        Assert.Equal("not_found", missing.Json().GetProperty("code").GetString());
    }

    // ---------------- TC6-U08 ----------------

    [Fact]
    public async Task TC6_U08_Equipment_history_shows_the_right_student_for_E1_and_E3_and_is_empty_for_E2()
    {
        string before = LabDatabase.Snapshot();
        int e1 = await EquipmentIdAsync("EQ-001");
        int e2 = await EquipmentIdAsync("EQ-002");
        int e3 = await EquipmentIdAsync("EQ-003");

        // E1: one ACTIVE record of S1.
        ApiResponse e1History = await GetAsync("/api/v1/equipment/" + e1 + "/borrowings");
        Assert.Equal(200, e1History.Status);
        Assert.Equal(1, e1History.Json().GetArrayLength());
        JsonElement e1Record = e1History.Json()[0];
        Assert.Equal(e1, e1Record.GetProperty("equipmentId").GetInt32());
        Assert.Equal("ST-100", e1Record.GetProperty("studentNumber").GetString());
        Assert.Equal("Active", e1Record.GetProperty("status").GetString());

        // E3: one RETURNED record of S1.
        ApiResponse e3History = await GetAsync("/api/v1/equipment/" + e3 + "/borrowings");
        Assert.Equal(200, e3History.Status);
        Assert.Equal(1, e3History.Json().GetArrayLength());
        JsonElement e3Record = e3History.Json()[0];
        Assert.Equal(e3, e3Record.GetProperty("equipmentId").GetInt32());
        Assert.Equal("ST-100", e3Record.GetProperty("studentNumber").GetString());
        Assert.Equal("Returned", e3Record.GetProperty("status").GetString());

        // E2: never borrowed: 200 and [].
        ApiResponse e2History = await GetAsync("/api/v1/equipment/" + e2 + "/borrowings");
        Assert.Equal(200, e2History.Status);
        Assert.Equal(0, e2History.Json().GetArrayLength());

        Assert.Equal(before, LabDatabase.Snapshot());
    }

    [Fact]
    public async Task TC6_U08_Missing_equipment_returns_404_for_the_history_and_for_the_item_itself()
    {
        ApiResponse history = await GetAsync("/api/v1/equipment/999999/borrowings");
        Assert.Equal(404, history.Status);
        Assert.Equal("not_found", history.Json().GetProperty("code").GetString());

        ApiResponse item = await GetAsync("/api/v1/equipment/999999");
        Assert.Equal(404, item.Status);
        Assert.Equal("not_found", item.Json().GetProperty("code").GetString());
    }

    [Fact]
    public async Task TC6_U08_Equipment_by_id_returns_the_item()
    {
        int e1 = await EquipmentIdAsync("EQ-001");

        ApiResponse response = await GetAsync("/api/v1/equipment/" + e1);

        Assert.Equal(200, response.Status);
        JsonElement item = response.Json();
        Assert.Equal(e1, item.GetProperty("id").GetInt32());
        Assert.Equal("EQ-001", item.GetProperty("equipmentCode").GetString());
        Assert.Equal("Camera", item.GetProperty("name").GetString());
        Assert.Equal("Canon camera", item.GetProperty("description").GetString());
        Assert.False(item.GetProperty("isAvailable").GetBoolean());
    }

    // ---------------- TC6-U09 ----------------
    // Fixture borrowings:  1 = S1/E1 (borrowed D-9, expected D-1, active)
    //                      2 = S1/E3 (borrowed D-8, expected D+2, returned)
    //                      3 = S3/E4 and 4 = S3/E5 (both borrowed D-7, expected D+2, active)

    private async Task<List<string>> SearchCodesAsync(string query)
    {
        ApiResponse response = await GetAsync("/api/v1/borrowings" + query);
        Assert.Equal(200, response.Status);
        return BorrowedCodesOf(response.Json());
    }

    [Fact]
    public async Task TC6_U09_Search_by_student_name_ignores_case_and_matches_part_of_the_name()
    {
        string before = LabDatabase.Snapshot();

        Assert.Equal(new List<string> { "EQ-001", "EQ-003" }, await SearchCodesAsync("?search=aDa"));
        Assert.Equal(new List<string> { "EQ-004", "EQ-005" }, await SearchCodesAsync("?search=CAR"));

        Assert.Equal(before, LabDatabase.Snapshot());
    }

    [Fact]
    public async Task TC6_U09_Search_by_student_number_matches_part_of_the_number()
    {
        Assert.Equal(new List<string> { "EQ-004", "EQ-005" }, await SearchCodesAsync("?search=st-30"));
        Assert.Equal(new List<string> { "EQ-001", "EQ-003" }, await SearchCodesAsync("?search=ST-100"));
    }

    [Fact]
    public async Task TC6_U09_Search_by_equipment_name_matches_part_of_the_name()
    {
        Assert.Equal(new List<string> { "EQ-004" }, await SearchCodesAsync("?search=tab"));
        Assert.Equal(new List<string> { "EQ-005" }, await SearchCodesAsync("?search=MICRO"));
    }

    [Fact]
    public async Task TC6_U09_Search_with_no_match_returns_200_and_an_empty_list()
    {
        ApiResponse response = await GetAsync("/api/v1/borrowings?search=zzzz-no-match");

        Assert.Equal(200, response.Status);
        Assert.Equal(0, response.Json().GetArrayLength());
    }

    [Fact]
    public async Task TC6_U09_Status_filters_return_the_right_sets_and_an_invalid_status_is_400()
    {
        Assert.Equal(new List<string> { "EQ-001", "EQ-004", "EQ-005" }, await SearchCodesAsync("?status=Active"));
        Assert.Equal(new List<string> { "EQ-003" }, await SearchCodesAsync("?status=Returned"));
        Assert.Equal(new List<string> { "EQ-001", "EQ-003", "EQ-004", "EQ-005" }, await SearchCodesAsync("?status=All"));

        ApiResponse invalid = await GetAsync("/api/v1/borrowings?status=banana");
        Assert.Equal(400, invalid.Status);
        Assert.Equal("invalid_query", invalid.Json().GetProperty("code").GetString());
    }

    [Fact]
    public async Task TC6_U09_Sort_by_borrowed_date_in_both_directions_with_borrowing_id_as_tie_break()
    {
        // Ascending: D-9 (E1), D-8 (E3), then E4 and E5 tied on D-7 -> lower borrowing Id first.
        Assert.Equal(new List<string> { "EQ-001", "EQ-003", "EQ-004", "EQ-005" },
            await SearchCodesAsync("?sort=borrowedDate&direction=asc"));

        // Descending: D-7 first (E4 then E5, tie broken by Id), then D-8, then D-9.
        Assert.Equal(new List<string> { "EQ-004", "EQ-005", "EQ-003", "EQ-001" },
            await SearchCodesAsync("?sort=borrowedDate&direction=desc"));
    }

    [Fact]
    public async Task TC6_U09_Sort_by_expected_return_date_in_both_directions_with_borrowing_id_as_tie_break()
    {
        // Ascending: D-1 (E1), then E3, E4, E5 all tied on D+2 -> by borrowing Id (2, 3, 4).
        Assert.Equal(new List<string> { "EQ-001", "EQ-003", "EQ-004", "EQ-005" },
            await SearchCodesAsync("?sort=expectedReturnDate&direction=asc"));

        // Descending: the D+2 group first (still E3, E4, E5 by Id), then D-1 (E1).
        Assert.Equal(new List<string> { "EQ-003", "EQ-004", "EQ-005", "EQ-001" },
            await SearchCodesAsync("?sort=expectedReturnDate&direction=desc"));
    }

    [Fact]
    public async Task TC6_U09_Combined_criteria_and_reset_to_the_default_list()
    {
        // search + status + sort together.
        Assert.Equal(new List<string> { "EQ-004", "EQ-005" },
            await SearchCodesAsync("?search=cara&status=Active&sort=expectedReturnDate&direction=desc"));
        Assert.Equal(new List<string> { "EQ-003" }, await SearchCodesAsync("?search=ada&status=Returned"));
        Assert.Equal(new List<string> { "EQ-001" }, await SearchCodesAsync("?search=ada&status=Active"));
        Assert.Empty(await SearchCodesAsync("?search=cara&status=Returned"));

        // "Reset": no criteria at all gives all four records again.
        Assert.Equal(4, (await SearchCodesAsync("")).Count);
    }

    [Fact]
    public async Task TC6_U09_Invalid_sort_field_or_direction_is_400()
    {
        ApiResponse badSort = await GetAsync("/api/v1/borrowings?sort=color");
        Assert.Equal(400, badSort.Status);

        ApiResponse badDirection = await GetAsync("/api/v1/borrowings?direction=sideways");
        Assert.Equal(400, badDirection.Status);
    }
}
