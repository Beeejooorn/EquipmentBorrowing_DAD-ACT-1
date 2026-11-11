namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;

// Turns a database error kind into a message for the user (BR-18).
// The messages never contain the connection string or password.
public static class DatabaseErrors
{
    // For save operations.
    // commitStarted = true means the error happened while PostgreSQL was committing.
    public static OperationResult ToResult(DatabaseErrorKind kind, bool commitStarted)
    {
        if (kind == DatabaseErrorKind.ConnectionFailed && commitStarted)
        {
            // We asked PostgreSQL to commit, but lost the connection before we heard back.
            // The transaction is still all-or-nothing, but we do not know which one happened,
            // so we must NOT say "nothing was saved" and must NOT retry automatically.
            return new OperationResult(ResultKind.UnknownOutcome,
                "The connection to the database was lost while saving, so the result is UNKNOWN. " +
                "The changes were either fully saved or not saved at all. " +
                "Refresh the list and check the current records before trying again.");
        }

        if (kind == DatabaseErrorKind.ConnectionFailed)
        {
            return new OperationResult(ResultKind.Failed,
                "Could not reach the PostgreSQL database. Nothing was saved. " +
                "Check that the database server is running, then try again.");
        }

        if (kind == DatabaseErrorKind.Conflict)
        {
            return new OperationResult(ResultKind.Conflict,
                "Another change to the same records was saved at the same time. Nothing was saved. " +
                "The data has been refreshed, please review it and try again.");
        }

        if (kind == DatabaseErrorKind.DuplicateValue)
        {
            return new OperationResult(ResultKind.Rejected,
                "The database rejected the change because that identifier is already used. Nothing was saved.");
        }

        if (kind == DatabaseErrorKind.InvalidReference)
        {
            return new OperationResult(ResultKind.NotFound,
                "A selected record no longer exists in the database. Nothing was saved. The data has been refreshed.");
        }

        if (kind == DatabaseErrorKind.ConstraintViolation)
        {
            return new OperationResult(ResultKind.Rejected,
                "The database rejected the data because it breaks a data rule. Nothing was saved.");
        }

        return new OperationResult(ResultKind.Failed,
            "An unexpected database error occurred. Nothing was saved.");
    }

    // For load operations (lists and histories).
    public static string ToLoadMessage(DatabaseErrorKind kind)
    {
        if (kind == DatabaseErrorKind.ConnectionFailed)
        {
            return "Could not load data: the PostgreSQL database cannot be reached. " +
                   "Check that the database server is running, then click Retry.";
        }

        return "Could not load data because of an unexpected database error. Click Retry to try again.";
    }
}
