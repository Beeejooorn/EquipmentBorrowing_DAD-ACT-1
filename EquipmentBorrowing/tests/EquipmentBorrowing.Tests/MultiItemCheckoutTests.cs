using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;

namespace EquipmentBorrowing.Tests;

// Appendix C - A06 (UC-06 Multi-Item Checkout, BR-09, BR-12, BR-15, BR-18).
// Every rejection test also checks that NOTHING was saved (no new rows, items still available).
public class MultiItemCheckoutTests : DatabaseTestBase
{
    private List<int> Items(params string[] codes)
    {
        List<int> ids = new List<int>();

        foreach (string code in codes)
        {
            ids.Add(EquipmentId(code));
        }

        return ids;
    }

    private void AssertNothingSaved(int borrowingsBefore, params string[] codesThatMustStayAvailable)
    {
        Assert.Equal(borrowingsBefore, CountBorrowings());

        foreach (string code in codesThatMustStayAvailable)
        {
            Assert.True(IsAvailable(code), code + " should still be available");
        }
    }

    [Fact]
    public async Task CheckOut_TwoAvailableItems_SavesOneBorrowingPerItem()
    {
        int dan = StudentId("2023-0004");

        OperationResult result = await BorrowService.CheckOutMultipleAsync(dan, Items("EQ-LAP-02", "EQ-PRJ-01"), Today.AddDays(7));

        Assert.Equal(ResultKind.Success, result.Kind);
        Assert.Equal(2, CountActiveBorrowings(dan));
        Assert.False(IsAvailable("EQ-LAP-02"));
        Assert.False(IsAvailable("EQ-PRJ-01"));

        using EquipmentBorrowingDbContext context = FreshContext();
        List<Borrowing> rows = context.Borrowings.Where(b => b.StudentId == dan).ToList();
        Assert.All(rows, b => Assert.Equal(Today, b.DateBorrowed));
        Assert.All(rows, b => Assert.Equal(Today.AddDays(7), b.ExpectedReturnDate));
    }

    [Fact]
    public async Task CheckOut_ExactlyAtTheLimitOfThree_Succeeds()
    {
        int ana = StudentId("2023-0001"); // already has 1 active borrowing

        OperationResult result = await BorrowService.CheckOutMultipleAsync(ana, Items("EQ-LAP-02", "EQ-PRJ-01"), Today.AddDays(7));

        Assert.Equal(ResultKind.Success, result.Kind);
        Assert.Equal(3, CountActiveBorrowings(ana));
    }

    [Fact]
    public async Task CheckOut_OverTheLimit_IsRejectedAndSavesNothing()
    {
        int ana = StudentId("2023-0001"); // 1 active + 3 requested = 4
        int before = CountBorrowings();

        OperationResult result = await BorrowService.CheckOutMultipleAsync(ana, Items("EQ-LAP-02", "EQ-PRJ-01", "EQ-TAB-01"), Today.AddDays(7));

        Assert.Equal(ResultKind.Rejected, result.Kind);
        AssertNothingSaved(before, "EQ-LAP-02", "EQ-PRJ-01", "EQ-TAB-01");
    }

    [Fact]
    public async Task CheckOut_DuplicateItem_IsRejectedNotIgnored()
    {
        int before = CountBorrowings();

        OperationResult result = await BorrowService.CheckOutMultipleAsync(StudentId("2023-0004"), Items("EQ-LAP-02", "EQ-LAP-02"), Today.AddDays(7));

        Assert.Equal(ResultKind.Rejected, result.Kind);
        Assert.Contains("more than once", result.Message);
        AssertNothingSaved(before, "EQ-LAP-02");
    }

    [Fact]
    public async Task CheckOut_FewerThanTwoItems_IsRejected()
    {
        OperationResult oneItem = await BorrowService.CheckOutMultipleAsync(StudentId("2023-0004"), Items("EQ-LAP-02"), Today.AddDays(7));
        OperationResult noItems = await BorrowService.CheckOutMultipleAsync(StudentId("2023-0004"), new List<int>(), Today.AddDays(7));

        Assert.Equal(ResultKind.Rejected, oneItem.Kind);
        Assert.Equal(ResultKind.Rejected, noItems.Kind);
        AssertNothingSaved(5, "EQ-LAP-02");
    }

    [Fact]
    public async Task CheckOut_OneUnavailableItem_RejectsTheWholeCheckout()
    {
        int before = CountBorrowings();

        // Laptop 02 is available, Camera 01 is not.
        OperationResult result = await BorrowService.CheckOutMultipleAsync(StudentId("2023-0004"), Items("EQ-LAP-02", "EQ-CAM-01"), Today.AddDays(7));

        Assert.Equal(ResultKind.Rejected, result.Kind);
        AssertNothingSaved(before, "EQ-LAP-02");
    }

    [Fact]
    public async Task CheckOut_MissingItem_ReturnsNotFoundAndSavesNothing()
    {
        List<int> ids = Items("EQ-LAP-02");
        ids.Add(99999);

        OperationResult result = await BorrowService.CheckOutMultipleAsync(StudentId("2023-0004"), ids, Today.AddDays(7));

        Assert.Equal(ResultKind.NotFound, result.Kind);
        AssertNothingSaved(5, "EQ-LAP-02");
    }

    [Fact]
    public async Task CheckOut_RestrictedOrMissingStudent_IsRejected()
    {
        OperationResult restricted = await BorrowService.CheckOutMultipleAsync(StudentId("2023-0003"), Items("EQ-LAP-02", "EQ-PRJ-01"), Today.AddDays(7));
        OperationResult missing = await BorrowService.CheckOutMultipleAsync(99999, Items("EQ-LAP-02", "EQ-PRJ-01"), Today.AddDays(7));

        Assert.Equal(ResultKind.Rejected, restricted.Kind);
        Assert.Equal(ResultKind.NotFound, missing.Kind);
        AssertNothingSaved(5, "EQ-LAP-02", "EQ-PRJ-01");
    }

    [Fact]
    public async Task CheckOut_InvalidExpectedDate_IsRejected()
    {
        OperationResult today = await BorrowService.CheckOutMultipleAsync(StudentId("2023-0004"), Items("EQ-LAP-02", "EQ-PRJ-01"), Today);
        OperationResult missing = await BorrowService.CheckOutMultipleAsync(StudentId("2023-0004"), Items("EQ-LAP-02", "EQ-PRJ-01"), null);

        Assert.Equal(ResultKind.Rejected, today.Kind);
        Assert.Equal(ResultKind.Rejected, missing.Kind);
        AssertNothingSaved(5, "EQ-LAP-02", "EQ-PRJ-01");
    }

    // The safe atomicity demonstration (test seam):
    // the first borrowing IS written to PostgreSQL inside the transaction, then a failure happens.
    // A fresh read must show that nothing at all was saved.
    [Fact]
    public async Task CheckOut_FailureAfterFirstWrite_RollsBackEverything()
    {
        FailureSimulator.Mode = SimulatedCheckoutFailure.AfterFirstWrite;
        int dan = StudentId("2023-0004");
        int before = CountBorrowings();

        OperationResult result = await BorrowService.CheckOutMultipleAsync(dan, Items("EQ-LAP-02", "EQ-PRJ-01"), Today.AddDays(7));

        Assert.Equal(ResultKind.Failed, result.Kind);
        Assert.Contains("rolled back", result.Message);
        Assert.Equal(0, CountActiveBorrowings(dan));
        AssertNothingSaved(before, "EQ-LAP-02", "EQ-PRJ-01");
    }

    // BR-18: the commit succeeded but the answer was "lost".
    // The service must report UNKNOWN (not "nothing saved"), and a fresh read shows
    // the transaction is still all-or-nothing (here: both rows are there).
    [Fact]
    public async Task CheckOut_LostCommitAcknowledgement_ReportsUnknownOutcome()
    {
        FailureSimulator.Mode = SimulatedCheckoutFailure.LostCommitAcknowledgement;
        int dan = StudentId("2023-0004");

        OperationResult result = await BorrowService.CheckOutMultipleAsync(dan, Items("EQ-LAP-02", "EQ-PRJ-01"), Today.AddDays(7));

        Assert.Equal(ResultKind.UnknownOutcome, result.Kind);
        Assert.Equal(2, CountActiveBorrowings(dan));
        Assert.False(IsAvailable("EQ-LAP-02"));
        Assert.False(IsAvailable("EQ-PRJ-01"));
    }

    // Single-item borrowing never uses the test seam.
    [Fact]
    public async Task SingleBorrow_IsNotAffectedByTheCheckoutTestSeam()
    {
        FailureSimulator.Mode = SimulatedCheckoutFailure.AfterFirstWrite;

        OperationResult result = await BorrowService.BorrowAsync(StudentId("2023-0004"), EquipmentId("EQ-LAP-02"), Today.AddDays(7));

        Assert.Equal(ResultKind.Success, result.Kind);
    }
}
