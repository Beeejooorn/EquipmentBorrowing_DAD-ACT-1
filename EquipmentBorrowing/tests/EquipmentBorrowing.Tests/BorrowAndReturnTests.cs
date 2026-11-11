using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;

namespace EquipmentBorrowing.Tests;

// Appendix C - A03 (UC-03 Borrow Equipment) and A05 (UC-05 Return Equipment).
// Sample data: Ana (1 active: Camera 01), Ben (1 active: Laptop 01), Carla (restricted), Dan (no history).
public class BorrowAndReturnTests : DatabaseTestBase
{
    // ---------------- A03: Borrow ----------------

    [Fact]
    public async Task Borrow_ValidRequest_SavesActiveBorrowingAndMarksItemUnavailable()
    {
        int dan = StudentId("2023-0004");
        int laptop2 = EquipmentId("EQ-LAP-02");

        OperationResult result = await BorrowService.BorrowAsync(dan, laptop2, Today.AddDays(7));

        Assert.Equal(ResultKind.Success, result.Kind);

        // Read back with a fresh context.
        using EquipmentBorrowingDbContext context = FreshContext();
        Borrowing saved = context.Borrowings.Single(b => b.StudentId == dan);
        Assert.Equal(laptop2, saved.EquipmentId);
        Assert.Equal(BorrowingStatus.Active, saved.Status);
        Assert.Equal(Today, saved.DateBorrowed);
        Assert.Equal(Today.AddDays(7), saved.ExpectedReturnDate);
        Assert.Null(saved.ReturnedAt);
        Assert.False(IsAvailable("EQ-LAP-02"));
    }

    [Fact]
    public async Task Borrow_RestrictedStudent_IsRejectedAndSavesNothing()
    {
        int carla = StudentId("2023-0003");
        int borrowingsBefore = CountBorrowings();

        OperationResult result = await BorrowService.BorrowAsync(carla, EquipmentId("EQ-LAP-02"), Today.AddDays(7));

        Assert.Equal(ResultKind.Rejected, result.Kind);
        Assert.Equal(borrowingsBefore, CountBorrowings());
        Assert.True(IsAvailable("EQ-LAP-02"));
    }

    [Fact]
    public async Task Borrow_UnavailableItem_IsRejected()
    {
        int borrowingsBefore = CountBorrowings();

        // Camera 01 is actively borrowed by Ana.
        OperationResult result = await BorrowService.BorrowAsync(StudentId("2023-0004"), EquipmentId("EQ-CAM-01"), Today.AddDays(7));

        Assert.Equal(ResultKind.Rejected, result.Kind);
        Assert.Equal(borrowingsBefore, CountBorrowings());
    }

    [Fact]
    public async Task Borrow_MissingStudentOrItem_ReturnsNotFound()
    {
        OperationResult missingStudent = await BorrowService.BorrowAsync(99999, EquipmentId("EQ-LAP-02"), Today.AddDays(7));
        OperationResult missingItem = await BorrowService.BorrowAsync(StudentId("2023-0004"), 99999, Today.AddDays(7));

        Assert.Equal(ResultKind.NotFound, missingStudent.Kind);
        Assert.Equal(ResultKind.NotFound, missingItem.Kind);
        Assert.Equal(5, CountBorrowings());
    }

    [Fact]
    public async Task Borrow_ExpectedDateTodayPastOrMissing_IsRejected()
    {
        int dan = StudentId("2023-0004");
        int laptop2 = EquipmentId("EQ-LAP-02");

        OperationResult sameDay = await BorrowService.BorrowAsync(dan, laptop2, Today);
        OperationResult pastDay = await BorrowService.BorrowAsync(dan, laptop2, Today.AddDays(-1));
        OperationResult noDate = await BorrowService.BorrowAsync(dan, laptop2, null);

        Assert.Equal(ResultKind.Rejected, sameDay.Kind);
        Assert.Equal(ResultKind.Rejected, pastDay.Kind);
        Assert.Equal(ResultKind.Rejected, noDate.Kind);
        Assert.Equal(0, CountActiveBorrowings(dan));
    }

    [Fact]
    public async Task Borrow_FourthActiveItem_IsRejected()
    {
        int dan = StudentId("2023-0004");

        Assert.Equal(ResultKind.Success, (await BorrowService.BorrowAsync(dan, EquipmentId("EQ-LAP-02"), Today.AddDays(7))).Kind);
        Assert.Equal(ResultKind.Success, (await BorrowService.BorrowAsync(dan, EquipmentId("EQ-PRJ-01"), Today.AddDays(7))).Kind);
        Assert.Equal(ResultKind.Success, (await BorrowService.BorrowAsync(dan, EquipmentId("EQ-TAB-01"), Today.AddDays(7))).Kind);

        OperationResult fourth = await BorrowService.BorrowAsync(dan, EquipmentId("EQ-MIC-01"), Today.AddDays(7));

        Assert.Equal(ResultKind.Rejected, fourth.Kind);
        Assert.Equal(3, CountActiveBorrowings(dan));
        Assert.True(IsAvailable("EQ-MIC-01"));
    }

    // ---------------- A05: Return ----------------

    [Fact]
    public async Task Return_ActiveBorrowing_MarksReturnedAndMakesItemAvailable()
    {
        int ana = StudentId("2023-0001");
        int camera = EquipmentId("EQ-CAM-01");
        int borrowingId;

        using (EquipmentBorrowingDbContext context = FreshContext())
        {
            borrowingId = context.Borrowings.Single(b => b.EquipmentId == camera && b.Status == BorrowingStatus.Active).Id;
        }

        OperationResult result = await ReturnService.ReturnAsync(borrowingId);

        Assert.Equal(ResultKind.Success, result.Kind);

        using (EquipmentBorrowingDbContext context = FreshContext())
        {
            Borrowing returned = context.Borrowings.Single(b => b.Id == borrowingId);
            Assert.Equal(BorrowingStatus.Returned, returned.Status);
            Assert.Equal((DateOnly?)Today, returned.ReturnedAt);
            Assert.Equal(ana, returned.StudentId);
        }

        Assert.True(IsAvailable("EQ-CAM-01"));
        Assert.Equal(5, CountBorrowings()); // history is kept, nothing deleted
    }

    [Fact]
    public async Task Return_AlreadyReturned_IsRejectedAndChangesNothing()
    {
        int borrowingId;
        DateOnly? originalReturnDate;

        using (EquipmentBorrowingDbContext context = FreshContext())
        {
            Borrowing alreadyReturned = context.Borrowings.First(b => b.Status == BorrowingStatus.Returned);
            borrowingId = alreadyReturned.Id;
            originalReturnDate = alreadyReturned.ReturnedAt;
        }

        OperationResult result = await ReturnService.ReturnAsync(borrowingId);

        Assert.Equal(ResultKind.Rejected, result.Kind);

        using (EquipmentBorrowingDbContext context = FreshContext())
        {
            Assert.Equal(originalReturnDate, context.Borrowings.Single(b => b.Id == borrowingId).ReturnedAt);
        }
    }

    [Fact]
    public async Task Return_MissingBorrowing_ReturnsNotFound()
    {
        OperationResult result = await ReturnService.ReturnAsync(99999);

        Assert.Equal(ResultKind.NotFound, result.Kind);
    }

    [Fact]
    public async Task Return_ByRestrictedStudent_IsStillAllowed()
    {
        int ben = StudentId("2023-0002");
        await StudentManagement.ChangeEligibilityAsync(ben, false);

        int borrowingId;
        using (EquipmentBorrowingDbContext context = FreshContext())
        {
            borrowingId = context.Borrowings.Single(b => b.StudentId == ben && b.Status == BorrowingStatus.Active).Id;
        }

        OperationResult result = await ReturnService.ReturnAsync(borrowingId);

        Assert.Equal(ResultKind.Success, result.Kind);
        Assert.Equal(0, CountActiveBorrowings(ben));
    }
}
