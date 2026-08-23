namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Domain;

public class BorrowResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public Borrowing? Borrowing { get; }

    public BorrowResult(bool isSuccess, string? errorMessage, Borrowing? borrowing)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        Borrowing = borrowing;
    }
}