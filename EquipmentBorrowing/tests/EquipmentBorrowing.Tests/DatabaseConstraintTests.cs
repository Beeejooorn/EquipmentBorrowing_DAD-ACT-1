using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EquipmentBorrowing.Tests;

// Appendix C - A17 (BR-16): the rules are enforced by PostgreSQL ITSELF.
// These tests skip the application completely and send plain SQL,
// the way a bug or another program could. PostgreSQL must still refuse the bad data.
public class DatabaseConstraintTests : DatabaseTestBase
{
    // Runs the SQL and returns the PostgreSQL error it caused (or null if it was accepted).
    private PostgresException? RunSql(string sql)
    {
        try
        {
            using EquipmentBorrowingDbContext context = FreshContext();
            context.Database.ExecuteSqlRaw(sql);
            return null;
        }
        catch (Exception ex)
        {
            Exception? current = ex;

            while (current != null)
            {
                if (current is PostgresException postgresError)
                {
                    return postgresError;
                }

                current = current.InnerException;
            }

            throw;
        }
    }

    [Fact]
    public void Database_RejectsDuplicateStudentNumber_IgnoringCaseAndSpaces()
    {
        PostgresException? error = RunSql(
            "INSERT INTO \"Students\" (\"StudentNumber\", \"Name\", \"IsAllowedToBorrow\") VALUES (' 2023-0001 ', 'Copy', true);");

        Assert.NotNull(error);
        Assert.Equal("23505", error.SqlState); // unique violation
        Assert.Equal("IX_Students_StudentNumberKey", error.ConstraintName);
    }

    [Fact]
    public void Database_RejectsDuplicateEquipmentCode_IgnoringCaseAndSpaces()
    {
        PostgresException? error = RunSql(
            "INSERT INTO \"Equipment\" (\"EquipmentCode\", \"Name\", \"IsAvailable\") VALUES ('eq-lap-02 ', 'Copy', true);");

        Assert.NotNull(error);
        Assert.Equal("23505", error.SqlState);
        Assert.Equal("IX_Equipment_EquipmentCodeKey", error.ConstraintName);
    }

    [Fact]
    public void Database_RejectsSecondActiveBorrowingForTheSameItem()
    {
        // Laptop 01 already has an active borrowing (Ben).
        int dan = StudentId("2023-0004");
        int laptop1 = EquipmentId("EQ-LAP-01");

        PostgresException? error = RunSql(
            "INSERT INTO \"Borrowings\" (\"StudentId\", \"EquipmentId\", \"DateBorrowed\", \"ExpectedReturnDate\", \"Status\") " +
            $"VALUES ({dan}, {laptop1}, CURRENT_DATE, CURRENT_DATE + 7, 'Active');");

        Assert.NotNull(error);
        Assert.Equal("23505", error.SqlState);
        Assert.Equal("IX_Borrowings_OneActivePerEquipment", error.ConstraintName);

        // And the application turns this into a "conflict", not a crash.
        Assert.Equal(DatabaseErrorKind.Conflict, ErrorClassifier.Classify(error));
    }

    [Fact]
    public void Database_AllowsManyReturnedBorrowingsForTheSameItem()
    {
        // The partial index only looks at Active rows, so history can grow.
        int dan = StudentId("2023-0004");
        int laptop1 = EquipmentId("EQ-LAP-01");

        PostgresException? error = RunSql(
            "INSERT INTO \"Borrowings\" (\"StudentId\", \"EquipmentId\", \"DateBorrowed\", \"ExpectedReturnDate\", \"Status\", \"ReturnedAt\") " +
            $"VALUES ({dan}, {laptop1}, CURRENT_DATE - 9, CURRENT_DATE - 2, 'Returned', CURRENT_DATE - 3);");

        Assert.Null(error);
    }

    [Fact]
    public void Database_RejectsBorrowingForMissingStudent()
    {
        int laptop2 = EquipmentId("EQ-LAP-02");

        PostgresException? error = RunSql(
            "INSERT INTO \"Borrowings\" (\"StudentId\", \"EquipmentId\", \"DateBorrowed\", \"ExpectedReturnDate\", \"Status\") " +
            $"VALUES (99999, {laptop2}, CURRENT_DATE, CURRENT_DATE + 7, 'Active');");

        Assert.NotNull(error);
        Assert.Equal("23503", error.SqlState); // foreign key violation
    }

    [Fact]
    public void Database_RejectsExpectedReturnDateNotAfterCheckout()
    {
        int dan = StudentId("2023-0004");
        int laptop2 = EquipmentId("EQ-LAP-02");

        PostgresException? error = RunSql(
            "INSERT INTO \"Borrowings\" (\"StudentId\", \"EquipmentId\", \"DateBorrowed\", \"ExpectedReturnDate\", \"Status\") " +
            $"VALUES ({dan}, {laptop2}, CURRENT_DATE, CURRENT_DATE, 'Active');");

        Assert.NotNull(error);
        Assert.Equal("23514", error.SqlState); // check violation
        Assert.Equal("CK_Borrowings_ExpectedAfterCheckout", error.ConstraintName);
    }

    [Fact]
    public void Database_RejectsReturnedStatusWithoutReturnDate()
    {
        int dan = StudentId("2023-0004");
        int laptop2 = EquipmentId("EQ-LAP-02");

        PostgresException? error = RunSql(
            "INSERT INTO \"Borrowings\" (\"StudentId\", \"EquipmentId\", \"DateBorrowed\", \"ExpectedReturnDate\", \"Status\") " +
            $"VALUES ({dan}, {laptop2}, CURRENT_DATE, CURRENT_DATE + 7, 'Returned');");

        Assert.NotNull(error);
        Assert.Equal("23514", error.SqlState);
        Assert.Equal("CK_Borrowings_StatusMatchesReturnDate", error.ConstraintName);
    }

    [Fact]
    public void Database_RejectsBlankOrMissingRequiredText()
    {
        PostgresException? blankName = RunSql(
            "INSERT INTO \"Students\" (\"StudentNumber\", \"Name\", \"IsAllowedToBorrow\") VALUES ('2023-0500', '   ', true);");
        PostgresException? missingNumber = RunSql(
            "INSERT INTO \"Students\" (\"StudentNumber\", \"Name\", \"IsAllowedToBorrow\") VALUES (NULL, 'No Number', true);");

        Assert.NotNull(blankName);
        Assert.Equal("23514", blankName.SqlState);
        Assert.NotNull(missingNumber);
        Assert.Equal("23502", missingNumber.SqlState); // not null violation
    }

    [Fact]
    public void Database_RefusesToDeleteAStudentThatHasHistory()
    {
        int ana = StudentId("2023-0001");

        PostgresException? error = RunSql($"DELETE FROM \"Students\" WHERE \"Id\" = {ana};");

        // ON DELETE RESTRICT keeps the history safe. PostgreSQL reports it as 23001 (restrict violation).
        Assert.NotNull(error);
        Assert.Equal("23001", error.SqlState);

        // The application treats it like any other broken reference, not as an unknown crash.
        Assert.Equal(DatabaseErrorKind.InvalidReference, ErrorClassifier.Classify(error));

        // And Ana is still there.
        Assert.Equal(ana, StudentId("2023-0001"));
    }
}
