namespace EquipmentBorrowing.Application.Models;

public enum BorrowingStatusFilter
{
    All,
    Active,
    Returned
}

public enum BorrowingSortField
{
    CheckoutDate,
    ExpectedReturnDate
}

// Everything the user chose on the Search Borrowings screen (UC-09, BR-17).
public class BorrowingSearchCriteria
{
    // Part of a student name, student number or equipment name. Empty = no text filter.
    public string? SearchText { get; set; }

    public BorrowingStatusFilter Status { get; set; } = BorrowingStatusFilter.All;

    public BorrowingSortField SortBy { get; set; } = BorrowingSortField.CheckoutDate;

    public bool SortDescending { get; set; } = false;
}
