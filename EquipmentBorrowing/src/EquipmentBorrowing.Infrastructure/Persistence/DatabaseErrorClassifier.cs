using System.Net.Sockets;
using EquipmentBorrowing.Application.Interfaces;
using Npgsql;

namespace EquipmentBorrowing.Infrastructure.Persistence;

// Looks at an exception thrown by EF Core / Npgsql and decides what kind of problem it is.
// EF Core usually wraps the real error, so we walk through the InnerException chain.
public class DatabaseErrorClassifier : IDatabaseErrorClassifier
{
    public DatabaseErrorKind Classify(Exception exception)
    {
        Exception? current = exception;

        while (current != null)
        {
            // An error answered by the PostgreSQL server itself (it has an SQLSTATE code).
            if (current is PostgresException postgresError)
            {
                return ClassifySqlState(postgresError);
            }

            // Any other Npgsql error means we could not talk to the server properly.
            if (current is NpgsqlException)
            {
                return DatabaseErrorKind.ConnectionFailed;
            }

            if (current is SocketException || current is IOException || current is TimeoutException)
            {
                return DatabaseErrorKind.ConnectionFailed;
            }

            current = current.InnerException;
        }

        return DatabaseErrorKind.Unknown;
    }

    private static DatabaseErrorKind ClassifySqlState(PostgresException error)
    {
        string code = error.SqlState;

        // 40001 = serialization failure, 40P01 = deadlock: another transaction got in the way.
        if (code == "40001" || code == "40P01")
        {
            return DatabaseErrorKind.Conflict;
        }

        // 23505 = unique violation.
        if (code == "23505")
        {
            // Two active borrowings for the same item means someone else issued it first.
            if (error.ConstraintName == "IX_Borrowings_OneActivePerEquipment")
            {
                return DatabaseErrorKind.Conflict;
            }

            return DatabaseErrorKind.DuplicateValue;
        }

        // 23503 = foreign key violation.
        if (code == "23503")
        {
            return DatabaseErrorKind.InvalidReference;
        }

        // 23514 = check violation, 23502 = not null violation, 22001 = text too long.
        if (code == "23514" || code == "23502" || code == "22001")
        {
            return DatabaseErrorKind.ConstraintViolation;
        }

        // 08xxx = connection problems, 57P0x = server shutting down,
        // 28xxx = login failed, 3D000 = database does not exist.
        if (code.StartsWith("08") || code.StartsWith("57P") || code.StartsWith("28") || code == "3D000")
        {
            return DatabaseErrorKind.ConnectionFailed;
        }

        return DatabaseErrorKind.Unknown;
    }
}
