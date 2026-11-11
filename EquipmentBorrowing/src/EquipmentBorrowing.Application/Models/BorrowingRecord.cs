namespace EquipmentBorrowing.Application.Models;

// A borrowing together with the student and equipment it belongs to.
// Used for display only (active list, histories and search results).
public class BorrowingRecord
{
    public int BorrowingId { get; set; }

    public int StudentId { get; set; }
    public string StudentNumber { get; set; } = "";
    public string StudentName { get; set; } = "";

    public int EquipmentId { get; set; }
    public string EquipmentCode { get; set; } = "";
    public string EquipmentName { get; set; } = "";

    public DateOnly DateBorrowed { get; set; }
    public DateOnly ExpectedReturnDate { get; set; }
    public DateOnly? ReturnedAt { get; set; }
    public BorrowingStatus Status { get; set; }

    // Text versions so the screen does not need converters.
    public string DateBorrowedText
    {
        get { return DateBorrowed.ToString("yyyy-MM-dd"); }
    }

    public string ExpectedReturnDateText
    {
        get { return ExpectedReturnDate.ToString("yyyy-MM-dd"); }
    }

    public string ReturnedAtText
    {
        get
        {
            if (ReturnedAt.HasValue)
            {
                return ReturnedAt.Value.ToString("yyyy-MM-dd");
            }
            return "-";
        }
    }

    public string StatusText
    {
        get { return Status.ToString(); }
    }
}
