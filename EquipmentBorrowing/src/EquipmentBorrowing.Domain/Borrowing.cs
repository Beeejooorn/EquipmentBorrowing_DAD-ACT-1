namespace EquipmentBorrowing.Domain;

public class Borrowing
{
    public int Id { get; }
    public int StudentId { get; }
    public int EquipmentId { get; }

    // All dates are local calendar dates (no time part), see BR-11 and BR-13.
    public DateOnly DateBorrowed { get; }
    public DateOnly ExpectedReturnDate { get; }
    public BorrowingStatus Status { get; private set; }
    public DateOnly? ReturnedAt { get; private set; }

    // Navigation properties so queries can read the related student and equipment.
    // They are filled in by EF Core when we load data, we never set them ourselves.
    public Student? Student { get; private set; }
    public Equipment? Equipment { get; private set; }

    public Borrowing(int Id, int StudentId, int EquipmentId, DateOnly DateBorrowed, DateOnly ExpectedReturnDate)
    {
        this.Id = Id;
        this.StudentId = StudentId;
        this.EquipmentId = EquipmentId;
        this.DateBorrowed = DateBorrowed;
        this.ExpectedReturnDate = ExpectedReturnDate;
        this.Status = BorrowingStatus.Active;
    }

    // The record is kept (never deleted), it just changes to Returned (BR-13, BR-14).
    public void MarkAsReturned(DateOnly returnedAt)
    {
        Status = BorrowingStatus.Returned;
        ReturnedAt = returnedAt;
    }
}
