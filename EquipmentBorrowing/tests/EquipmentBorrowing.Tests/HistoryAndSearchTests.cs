using EquipmentBorrowing.Application.Models;

namespace EquipmentBorrowing.Tests;

// Appendix C - A07 (UC-07), A08 (UC-08), A09 (UC-09, BR-17, BR-19).
// Sample data (5 borrowings):
//   Ana:   Laptop 01 (Returned), Camera 01 (Active)
//   Ben:   Projector 01 (Returned), Laptop 01 (Active)
//   Carla: Tablet 01 (Returned)
//   Dan:   nothing                 Microphone 01: never borrowed
public class HistoryAndSearchTests : DatabaseTestBase
{
    // ---------------- A07: student history ----------------

    [Fact]
    public async Task StudentHistory_ShowsOnlyThatStudentsActiveAndReturnedRecords()
    {
        int ana = StudentId("2023-0001");

        QueryResult<BorrowingRecord> result = await BorrowingQueries.GetStudentHistoryAsync(ana);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, r => Assert.Equal(ana, r.StudentId));
        Assert.Contains(result.Items, r => r.Status == BorrowingStatus.Active);
        Assert.Contains(result.Items, r => r.Status == BorrowingStatus.Returned);
    }

    [Fact]
    public async Task StudentHistory_StudentWithNoBorrowings_IsEmptyNotAnError()
    {
        QueryResult<BorrowingRecord> result = await BorrowingQueries.GetStudentHistoryAsync(StudentId("2023-0004"));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task StudentHistory_MissingStudent_IsNotFound()
    {
        QueryResult<BorrowingRecord> result = await BorrowingQueries.GetStudentHistoryAsync(99999);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsNotFound);
    }

    // ---------------- A08: equipment history ----------------

    [Fact]
    public async Task EquipmentHistory_ShowsOnlyThatItemAcrossDifferentStudents()
    {
        int laptop1 = EquipmentId("EQ-LAP-01");

        QueryResult<BorrowingRecord> result = await BorrowingQueries.GetEquipmentHistoryAsync(laptop1);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, r => Assert.Equal(laptop1, r.EquipmentId));
        Assert.Contains(result.Items, r => r.StudentName == "Ana Reyes" && r.Status == BorrowingStatus.Returned);
        Assert.Contains(result.Items, r => r.StudentName == "Ben Cruz" && r.Status == BorrowingStatus.Active);
    }

    [Fact]
    public async Task EquipmentHistory_NeverBorrowedItem_IsEmptyNotAnError()
    {
        QueryResult<BorrowingRecord> result = await BorrowingQueries.GetEquipmentHistoryAsync(EquipmentId("EQ-MIC-01"));

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task EquipmentHistory_MissingItem_IsNotFound()
    {
        QueryResult<BorrowingRecord> result = await BorrowingQueries.GetEquipmentHistoryAsync(99999);

        Assert.True(result.IsNotFound);
    }

    // ---------------- A09: search, filter, sort ----------------

    private async Task<List<BorrowingRecord>> Search(string? text, BorrowingStatusFilter status,
        BorrowingSortField sortBy = BorrowingSortField.CheckoutDate, bool descending = false)
    {
        BorrowingSearchCriteria criteria = new BorrowingSearchCriteria();
        criteria.SearchText = text;
        criteria.Status = status;
        criteria.SortBy = sortBy;
        criteria.SortDescending = descending;

        QueryResult<BorrowingRecord> result = await BorrowingQueries.SearchBorrowingsAsync(criteria);
        Assert.True(result.IsSuccess);
        return result.Items;
    }

    [Fact]
    public async Task Search_PartialStudentName_IsCaseInsensitiveAndTrimmed()
    {
        List<BorrowingRecord> results = await Search("   aNa  ", BorrowingStatusFilter.All);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("Ana Reyes", r.StudentName));
    }

    [Fact]
    public async Task Search_ByStudentNumber_FindsThatStudent()
    {
        List<BorrowingRecord> results = await Search("2023-0002", BorrowingStatusFilter.All);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("Ben Cruz", r.StudentName));
    }

    [Fact]
    public async Task Search_ByEquipmentName_FindsThatItem()
    {
        List<BorrowingRecord> results = await Search("LAPTOP", BorrowingStatusFilter.All);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("Laptop 01", r.EquipmentName));
    }

    [Fact]
    public async Task Search_StatusFilters_ReturnActiveReturnedOrAll()
    {
        Assert.Equal(2, (await Search(null, BorrowingStatusFilter.Active)).Count);
        Assert.Equal(3, (await Search(null, BorrowingStatusFilter.Returned)).Count);
        Assert.Equal(5, (await Search(null, BorrowingStatusFilter.All)).Count);
    }

    [Fact]
    public async Task Search_TextAndStatusCombined_UsesBothCriteria()
    {
        List<BorrowingRecord> results = await Search("laptop", BorrowingStatusFilter.Returned);

        BorrowingRecord only = Assert.Single(results);
        Assert.Equal("Ana Reyes", only.StudentName);
    }

    [Fact]
    public async Task Search_SortByCheckoutDateDescending_NewestFirst()
    {
        List<BorrowingRecord> results = await Search(null, BorrowingStatusFilter.All, BorrowingSortField.CheckoutDate, true);

        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].DateBorrowed >= results[i].DateBorrowed);
        }
    }

    [Fact]
    public async Task Search_SortByExpectedReturnDateAscending_OldestFirst()
    {
        List<BorrowingRecord> results = await Search(null, BorrowingStatusFilter.All, BorrowingSortField.ExpectedReturnDate, false);

        for (int i = 1; i < results.Count; i++)
        {
            Assert.True(results[i - 1].ExpectedReturnDate <= results[i].ExpectedReturnDate);
        }
    }

    [Fact]
    public async Task Search_SameDates_AreOrderedByBorrowingIdAsTieBreaker()
    {
        // Two borrowings with exactly the same checkout and expected dates.
        List<int> ids = new List<int> { EquipmentId("EQ-LAP-02"), EquipmentId("EQ-PRJ-01") };
        await BorrowService.CheckOutMultipleAsync(StudentId("2023-0004"), ids, Today.AddDays(7));

        List<BorrowingRecord> ascending = await Search("Dan", BorrowingStatusFilter.All, BorrowingSortField.CheckoutDate, false);
        List<BorrowingRecord> descending = await Search("Dan", BorrowingStatusFilter.All, BorrowingSortField.CheckoutDate, true);

        Assert.Equal(2, ascending.Count);
        Assert.True(ascending[0].BorrowingId < ascending[1].BorrowingId);
        Assert.True(descending[0].BorrowingId < descending[1].BorrowingId);
    }

    [Fact]
    public async Task Search_NoMatches_IsAnEmptySuccessNotAnError()
    {
        List<BorrowingRecord> results = await Search("zzz-no-such-name", BorrowingStatusFilter.All);

        Assert.Empty(results);
    }
}
