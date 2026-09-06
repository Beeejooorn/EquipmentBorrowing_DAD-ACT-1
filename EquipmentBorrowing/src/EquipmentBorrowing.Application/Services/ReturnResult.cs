namespace EquipmentBorrowing.Application.Services;

public class ReturnResult
{
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }

    public ReturnResult(bool isSuccess, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }
}