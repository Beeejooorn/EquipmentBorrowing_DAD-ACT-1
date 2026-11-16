using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// All tests share ONE disposable PostgreSQL database, so they must run one after the other.
// (Every test still starts from a freshly reset database, so no test depends on another one.)
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace EquipmentBorrowing.Lab6.Tests.Support;

// Creates, resets and reads the DISPOSABLE Lab 6 test database "equipmentborrowing_lab6_test".
//
// - It uses the same PostgreSQL server, user and password as the app (from user-secrets or the
//   environment variable), but a different database name, so real data is never touched.
// - No password is stored in this repository.
public static class LabDatabase
{
    public const string DatabaseName = "equipmentborrowing_lab6_test";

    // PostgreSQL error codes (SqlState) that the tests look for.
    public const string UniqueViolation = "23505";
    public const string CheckViolation = "23514";
    public const string ForeignKeyViolation = "23503";

    // D = the documented test date: TODAY (local calendar date), read once when the tests start.
    // The app itself uses today's date as the checkout date (BR-11), so every fixture date is
    // written relative to D (D-9, D+2, ...). If a test run crosses midnight, run it again.
    public static readonly DateOnly D = DateOnly.FromDateTime(DateTime.Now);

    private static readonly object _lock = new object();
    private static bool _schemaReady = false;

    // ---------- connection strings ----------

    // The connection string the API under test uses (normal connection pooling).
    public static string GetConnectionString()
    {
        NpgsqlConnectionStringBuilder builder =
            new NpgsqlConnectionStringBuilder(DatabaseSettings.GetConnectionString());
        builder.Database = DatabaseName;
        return builder.ConnectionString;
    }

    // Used only for checking results. Pooling is OFF, so every check opens a brand new
    // PostgreSQL connection that is completely independent from the API's connections.
    public static string GetFreshConnectionString()
    {
        NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder(GetConnectionString());
        builder.Pooling = false;
        return builder.ConnectionString;
    }

    // A BRAND NEW DbContext on a BRAND NEW connection. Always put it in a "using".
    public static EquipmentBorrowingDbContext NewFreshContext()
    {
        DbContextOptions<EquipmentBorrowingDbContext> options =
            new DbContextOptionsBuilder<EquipmentBorrowingDbContext>()
                .UseNpgsql(GetFreshConnectionString())
                .Options;

        return new EquipmentBorrowingDbContext(options);
    }

    // ---------- reset and fixtures ----------

    // Called at the start of every test: empty database + the same S1-S4 / E1-E7 fixtures.
    public static void ResetAndSeed()
    {
        lock (_lock)
        {
            // Safety check: never run the reset on a database that is not the Lab 6 test database.
            if (!DatabaseName.EndsWith("_lab6_test"))
            {
                throw new InvalidOperationException("Refusing to reset a database that is not a Lab 6 test database.");
            }

            using EquipmentBorrowingDbContext context = NewFreshContext();

            // Only once per test run: drop the test database and build it again from the migrations.
            if (!_schemaReady)
            {
                context.Database.EnsureDeleted();
                context.Database.Migrate();
                _schemaReady = true;
            }

            // Empty all tables and restart the Ids at 1.
            context.Database.ExecuteSqlRaw(
                "TRUNCATE \"Borrowings\", \"Students\", \"Equipment\" RESTART IDENTITY CASCADE;");

            SeedFixtures(context);
        }
    }

    // The fixtures from the Lab 6 Test Case Suite. All names are fictitious.
    //   S1 ST-100 Ada   eligible    E1 EQ-001 Camera      unavailable  S1 active   borrowed D-9, expected D-1
    //   S2 ST-200 Ben   restricted  E2 EQ-002 Laptop      available
    //   S3 ST-300 Cara  eligible    E3 EQ-003 Projector   available    S1 returned D-2, borrowed D-8, expected D+2
    //   S4 ST-400 Dani  eligible    E4 EQ-004 Tablet      unavailable  S3 active   borrowed D-7, expected D+2
    //                               E5 EQ-005 Microphone  unavailable  S3 active   borrowed D-7, expected D+2
    //                               E6 EQ-006 Tripod      available
    //                               E7 EQ-007 Speaker     available
    private static void SeedFixtures(EquipmentBorrowingDbContext context)
    {
        Student s1 = new Student(0, "ST-100", "Ada", "ada@example.test", true);
        Student s2 = new Student(0, "ST-200", "Ben", "ben@example.test", false);
        Student s3 = new Student(0, "ST-300", "Cara", "cara@example.test", true);
        Student s4 = new Student(0, "ST-400", "Dani", null, true);

        context.Students.Add(s1);
        context.Students.Add(s2);
        context.Students.Add(s3);
        context.Students.Add(s4);

        Equipment e1 = new Equipment(0, "EQ-001", "Camera", "Canon camera", false);
        Equipment e2 = new Equipment(0, "EQ-002", "Laptop", "Dell laptop", true);
        Equipment e3 = new Equipment(0, "EQ-003", "Projector", "Epson projector", true);
        Equipment e4 = new Equipment(0, "EQ-004", "Tablet", "10 inch tablet", false);
        Equipment e5 = new Equipment(0, "EQ-005", "Microphone", null, false);
        Equipment e6 = new Equipment(0, "EQ-006", "Tripod", null, true);
        Equipment e7 = new Equipment(0, "EQ-007", "Speaker", "Bluetooth speaker", true);

        context.Equipment.Add(e1);
        context.Equipment.Add(e2);
        context.Equipment.Add(e3);
        context.Equipment.Add(e4);
        context.Equipment.Add(e5);
        context.Equipment.Add(e6);
        context.Equipment.Add(e7);

        // Save first so the students and items get their Ids (1, 2, 3 ... because of RESTART IDENTITY).
        context.SaveChanges();

        // Borrowings are saved one by one so their Ids are always 1, 2, 3, 4 in this order.
        Borrowing b1 = new Borrowing(0, s1.Id, e1.Id, D.AddDays(-9), D.AddDays(-1));
        context.Borrowings.Add(b1);
        context.SaveChanges();

        Borrowing b2 = new Borrowing(0, s1.Id, e3.Id, D.AddDays(-8), D.AddDays(2));
        b2.MarkAsReturned(D.AddDays(-2));
        context.Borrowings.Add(b2);
        context.SaveChanges();

        Borrowing b3 = new Borrowing(0, s3.Id, e4.Id, D.AddDays(-7), D.AddDays(2));
        context.Borrowings.Add(b3);
        context.SaveChanges();

        Borrowing b4 = new Borrowing(0, s3.Id, e5.Id, D.AddDays(-7), D.AddDays(2));
        context.Borrowings.Add(b4);
        context.SaveChanges();
    }

    // ---------- fresh independent reads (every call opens a new connection) ----------

    public static int CountStudents()
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        return context.Students.Count();
    }

    public static int CountEquipment()
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        return context.Equipment.Count();
    }

    public static int CountBorrowings()
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        return context.Borrowings.Count();
    }

    public static int CountActiveBorrowings(int studentId)
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        return context.Borrowings.Count(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active);
    }

    public static int CountActiveForEquipment(int equipmentId)
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        return context.Borrowings.Count(b => b.EquipmentId == equipmentId && b.Status == BorrowingStatus.Active);
    }

    public static Student? FindStudent(string studentNumber)
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        return context.Students.AsNoTracking().FirstOrDefault(s => s.StudentNumber == studentNumber);
    }

    public static Equipment? FindEquipment(string equipmentCode)
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        return context.Equipment.AsNoTracking().FirstOrDefault(e => e.EquipmentCode == equipmentCode);
    }

    public static Borrowing? FindBorrowing(int borrowingId)
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        return context.Borrowings.AsNoTracking().FirstOrDefault(b => b.Id == borrowingId);
    }

    public static List<Borrowing> AllBorrowings()
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        return context.Borrowings.AsNoTracking().OrderBy(b => b.Id).ToList();
    }

    public static List<Borrowing> BorrowingsOfStudent(int studentId)
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        return context.Borrowings.AsNoTracking().Where(b => b.StudentId == studentId).OrderBy(b => b.Id).ToList();
    }

    public static List<Borrowing> BorrowingsOfEquipment(int equipmentId)
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        return context.Borrowings.AsNoTracking().Where(b => b.EquipmentId == equipmentId).OrderBy(b => b.Id).ToList();
    }

    // Removes every row (used by tests that need an EMPTY database, for example "empty catalog").
    public static void EmptyAllTables()
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        context.Database.ExecuteSqlRaw(
            "TRUNCATE \"Borrowings\", \"Students\", \"Equipment\" RESTART IDENTITY CASCADE;");
    }

    // All rows of all three tables as one text. Take it BEFORE and AFTER a read-only request:
    // if the two texts are equal, no row at all was changed ("Expected Database State: No changes").
    public static string Snapshot()
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();
        System.Text.StringBuilder text = new System.Text.StringBuilder();

        foreach (Student s in context.Students.AsNoTracking().OrderBy(x => x.Id))
        {
            text.AppendLine($"S|{s.Id}|{s.StudentNumber}|{s.Name}|{s.ContactInfo}|{s.IsAllowedToBorrow}");
        }

        foreach (Equipment e in context.Equipment.AsNoTracking().OrderBy(x => x.Id))
        {
            text.AppendLine($"E|{e.Id}|{e.EquipmentCode}|{e.Name}|{e.Description}|{e.IsAvailable}");
        }

        foreach (Borrowing b in context.Borrowings.AsNoTracking().OrderBy(x => x.Id))
        {
            text.AppendLine($"B|{b.Id}|{b.StudentId}|{b.EquipmentId}|{b.DateBorrowed}|{b.ExpectedReturnDate}|{b.Status}|{b.ReturnedAt}");
        }

        return text.ToString();
    }

    // ---------- direct SQL (to try to bypass the API and hit the real PostgreSQL rules) ----------

    // Runs one SQL statement directly on PostgreSQL.
    // Returns the PostgreSQL error if the database REJECTED it, or null if it was accepted.
    public static PostgresException? TryRawSql(string sql)
    {
        using EquipmentBorrowingDbContext context = NewFreshContext();

        try
        {
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

    // "2026-11-16" style text, always with the same format on every computer.
    public static string Iso(DateOnly date)
    {
        return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }
}
