namespace EquipmentBorrowing.Application.Models;

// The different ways a save operation (register, update, borrow, return...) can end.
public enum ResultKind
{
    Success,         // the change was saved
    NoChange,        // nothing needed to change (e.g. student is already restricted)
    Rejected,        // a business rule or validation rule said no, nothing was saved
    NotFound,        // the selected record no longer exists (missing or stale selection)
    Conflict,        // another change happened at the same time, nothing was saved
    Failed,          // a known database failure, nothing was saved
    UnknownOutcome   // the connection was lost while committing, the result must be checked (BR-18)
}

// Every save operation in the application services returns one of these,
// so the ViewModels never have to catch database exceptions themselves.
public class OperationResult
{
    public ResultKind Kind { get; }
    public string Message { get; }

    // The Id of a newly created record (used after registering a student or item).
    public int? RecordId { get; }

    public bool IsSuccess
    {
        get { return Kind == ResultKind.Success || Kind == ResultKind.NoChange; }
    }

    public OperationResult(ResultKind kind, string message, int? recordId = null)
    {
        Kind = kind;
        Message = message;
        RecordId = recordId;
    }

    public static OperationResult Success(string message, int? recordId = null)
    {
        return new OperationResult(ResultKind.Success, message, recordId);
    }

    public static OperationResult NoChange(string message)
    {
        return new OperationResult(ResultKind.NoChange, message);
    }

    public static OperationResult Rejected(string message)
    {
        return new OperationResult(ResultKind.Rejected, message);
    }

    public static OperationResult NotFound(string message)
    {
        return new OperationResult(ResultKind.NotFound, message);
    }
}
