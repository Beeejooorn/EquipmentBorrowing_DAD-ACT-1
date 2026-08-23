namespace EquipmentBorrowing.Domain;

public class Borrowing
{
    public int Id { get; }
    public int StudentId { get; }
    public int EquipmentId { get; }
    public DateTime DateBorrowed { get; }
    public DateTime ExpectedReturnDate { get; }
    public BorrowingStatus Status { get; private set; }

    public Borrowing(int Id, int StudentId, int EquipmentId, DateTime DateBorrowed, DateTime ExpectedReturnDate)
    {
        this.Id = Id;
        this.StudentId = StudentId;
        this.EquipmentId = EquipmentId;
        this.DateBorrowed = DateBorrowed;
        this.ExpectedReturnDate = ExpectedReturnDate;
        this.Status = BorrowingStatus.Active;
    }

    public void MarkAsReturned()
    {
        this.Status = BorrowingStatus.Returned;
    }
}   