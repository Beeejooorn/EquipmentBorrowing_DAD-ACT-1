using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;

namespace EquipmentBorrowing.Tests;

// Appendix C - A12 (UC-12), A13 (UC-13), A14 (UC-14), A15 (UC-15), A16 (UC-16).
public class StudentAndEquipmentManagementTests : DatabaseTestBase
{
    // ---------------- A12: register student ----------------

    [Fact]
    public async Task RegisterStudent_Valid_SavesTrimmedEligibleStudentWithNullContact()
    {
        OperationResult result = await StudentManagement.RegisterStudentAsync("  2023-0099 ", "  Eva Garcia ", "   ");

        Assert.Equal(ResultKind.Success, result.Kind);
        Assert.NotNull(result.RecordId);

        using EquipmentBorrowingDbContext context = FreshContext();
        Student saved = context.Students.Single(s => s.Id == result.RecordId);
        Assert.Equal("2023-0099", saved.StudentNumber);
        Assert.Equal("Eva Garcia", saved.Name);
        Assert.Null(saved.ContactInfo);
        Assert.True(saved.IsAllowedToBorrow);
    }

    [Fact]
    public async Task RegisterStudent_DuplicateNumberWithDifferentCaseOrSpaces_IsRejected()
    {
        await StudentManagement.RegisterStudentAsync("ab-100", "First Student", null);

        OperationResult withSpaces = await StudentManagement.RegisterStudentAsync(" 2023-0001 ", "Someone", null);
        OperationResult otherCase = await StudentManagement.RegisterStudentAsync("AB-100", "Someone Else", null);

        Assert.Equal(ResultKind.Rejected, withSpaces.Kind);
        Assert.Equal(ResultKind.Rejected, otherCase.Kind);

        using EquipmentBorrowingDbContext context = FreshContext();
        Assert.Equal(5, context.Students.Count()); // 4 sample students + ab-100
    }

    [Fact]
    public async Task RegisterStudent_BlankRequiredFields_IsRejected()
    {
        OperationResult blankNumber = await StudentManagement.RegisterStudentAsync("   ", "Name", null);
        OperationResult blankName = await StudentManagement.RegisterStudentAsync("2023-0100", "  ", null);

        Assert.Equal(ResultKind.Rejected, blankNumber.Kind);
        Assert.Equal(ResultKind.Rejected, blankName.Kind);

        using EquipmentBorrowingDbContext context = FreshContext();
        Assert.Equal(4, context.Students.Count());
    }

    // ---------------- A13: update student ----------------

    [Fact]
    public async Task UpdateStudent_ChangesDetailsAndKeepsBorrowingHistory()
    {
        int ana = StudentId("2023-0001");

        OperationResult result = await StudentManagement.UpdateStudentAsync(ana, "2023-1001", "Ana Reyes-Cruz", "");

        Assert.Equal(ResultKind.Success, result.Kind);

        using EquipmentBorrowingDbContext context = FreshContext();
        Student saved = context.Students.Single(s => s.Id == ana);
        Assert.Equal("2023-1001", saved.StudentNumber);
        Assert.Equal("Ana Reyes-Cruz", saved.Name);
        Assert.Null(saved.ContactInfo); // cleared optional field is stored as null
        Assert.Equal(2, context.Borrowings.Count(b => b.StudentId == ana));
    }

    [Fact]
    public async Task UpdateStudent_NumberOfAnotherStudent_IsRejected()
    {
        int ana = StudentId("2023-0001");

        OperationResult result = await StudentManagement.UpdateStudentAsync(ana, "2023-0002 ", "Ana Reyes", null);

        Assert.Equal(ResultKind.Rejected, result.Kind);
        Assert.Equal(ana, StudentId("2023-0001"));
    }

    [Fact]
    public async Task UpdateStudent_KeepingItsOwnNumber_IsAllowed()
    {
        int ana = StudentId("2023-0001");

        OperationResult result = await StudentManagement.UpdateStudentAsync(ana, "2023-0001", "Ana M. Reyes", "ana@campus.edu");

        Assert.Equal(ResultKind.Success, result.Kind);
    }

    [Fact]
    public async Task UpdateStudent_MissingStudent_ReturnsNotFound()
    {
        OperationResult result = await StudentManagement.UpdateStudentAsync(99999, "X-1", "Nobody", null);

        Assert.Equal(ResultKind.NotFound, result.Kind);
    }

    // ---------------- A14: change eligibility ----------------

    [Fact]
    public async Task RestrictStudent_BlocksNewBorrowingButKeepsActiveBorrowings()
    {
        int ana = StudentId("2023-0001");

        OperationResult restrict = await StudentManagement.ChangeEligibilityAsync(ana, false);
        OperationResult borrow = await BorrowService.BorrowAsync(ana, EquipmentId("EQ-LAP-02"), Today.AddDays(7));

        Assert.Equal(ResultKind.Success, restrict.Kind);
        Assert.Equal(ResultKind.Rejected, borrow.Kind);
        Assert.Equal(1, CountActiveBorrowings(ana)); // the existing Camera 01 borrowing is untouched
    }

    [Fact]
    public async Task PermitStudent_AllowsBorrowingAgain()
    {
        int carla = StudentId("2023-0003");

        OperationResult permit = await StudentManagement.ChangeEligibilityAsync(carla, true);
        OperationResult borrow = await BorrowService.BorrowAsync(carla, EquipmentId("EQ-LAP-02"), Today.AddDays(7));

        Assert.Equal(ResultKind.Success, permit.Kind);
        Assert.Equal(ResultKind.Success, borrow.Kind);
    }

    [Fact]
    public async Task ChangeEligibility_ToTheSameStatus_ReturnsNoChange()
    {
        OperationResult alreadyRestricted = await StudentManagement.ChangeEligibilityAsync(StudentId("2023-0003"), false);
        OperationResult alreadyEligible = await StudentManagement.ChangeEligibilityAsync(StudentId("2023-0004"), true);

        Assert.Equal(ResultKind.NoChange, alreadyRestricted.Kind);
        Assert.Equal(ResultKind.NoChange, alreadyEligible.Kind);
    }

    // ---------------- A15: register equipment ----------------

    [Fact]
    public async Task RegisterEquipment_Valid_SavesAvailableItemWithNullDescription()
    {
        OperationResult result = await EquipmentManagement.RegisterEquipmentAsync(" EQ-SPK-01 ", " Speaker 01 ", "  ");

        Assert.Equal(ResultKind.Success, result.Kind);

        using EquipmentBorrowingDbContext context = FreshContext();
        Equipment saved = context.Equipment.Single(e => e.Id == result.RecordId);
        Assert.Equal("EQ-SPK-01", saved.EquipmentCode);
        Assert.Equal("Speaker 01", saved.Name);
        Assert.Null(saved.Description);
        Assert.True(saved.IsAvailable);
    }

    [Fact]
    public async Task RegisterEquipment_DuplicateCodeWithDifferentCaseOrSpaces_IsRejected()
    {
        OperationResult result = await EquipmentManagement.RegisterEquipmentAsync("  eq-lap-02 ", "Another Laptop", null);

        Assert.Equal(ResultKind.Rejected, result.Kind);

        using EquipmentBorrowingDbContext context = FreshContext();
        Assert.Equal(6, context.Equipment.Count());
    }

    [Fact]
    public async Task RegisterEquipment_BlankRequiredFields_IsRejected()
    {
        OperationResult blankCode = await EquipmentManagement.RegisterEquipmentAsync("", "Name", null);
        OperationResult blankName = await EquipmentManagement.RegisterEquipmentAsync("EQ-X-01", "   ", null);

        Assert.Equal(ResultKind.Rejected, blankCode.Kind);
        Assert.Equal(ResultKind.Rejected, blankName.Kind);
    }

    // ---------------- A16: update equipment ----------------

    [Fact]
    public async Task UpdateEquipment_ChangesDetailsButNeverAvailabilityOrHistory()
    {
        int laptop1 = EquipmentId("EQ-LAP-01"); // currently borrowed (unavailable)

        OperationResult result = await EquipmentManagement.UpdateEquipmentAsync(laptop1, "EQ-LAP-01", "Laptop 01 (Dell)", "Dell Latitude 5440, 14 inch");

        Assert.Equal(ResultKind.Success, result.Kind);

        using EquipmentBorrowingDbContext context = FreshContext();
        Equipment saved = context.Equipment.Single(e => e.Id == laptop1);
        Assert.Equal("Laptop 01 (Dell)", saved.Name);
        Assert.False(saved.IsAvailable); // still borrowed, editing does not change availability
        Assert.Equal(2, context.Borrowings.Count(b => b.EquipmentId == laptop1));
        Assert.Equal(1, context.Borrowings.Count(b => b.EquipmentId == laptop1 && b.Status == BorrowingStatus.Active));
    }

    [Fact]
    public async Task UpdateEquipment_CodeOfAnotherItem_IsRejected()
    {
        OperationResult result = await EquipmentManagement.UpdateEquipmentAsync(EquipmentId("EQ-LAP-01"), "EQ-LAP-02", "Laptop 01", null);

        Assert.Equal(ResultKind.Rejected, result.Kind);
    }

    [Fact]
    public async Task UpdateEquipment_MissingItem_ReturnsNotFound()
    {
        OperationResult result = await EquipmentManagement.UpdateEquipmentAsync(99999, "EQ-NONE", "Nothing", null);

        Assert.Equal(ResultKind.NotFound, result.Kind);
    }
}
