using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Infrastructure;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

// The tests use the database server one test at a time (they share one test database).
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace EquipmentBorrowing.Tests;

// Creates and resets the DISPOSABLE test database "equipmentborrowing_test".
// It uses the same server, user and password as the app (from user-secrets),
// but a different database name, so the real data is never touched.
public static class TestDatabase
{
    public const string DatabaseName = "equipmentborrowing_test";

    private static readonly object _lock = new object();
    private static bool _schemaCreated = false;

    public static string GetConnectionString()
    {
        var builder = new NpgsqlConnectionStringBuilder(DatabaseSettings.GetConnectionString());
        builder.Database = DatabaseName;
        return builder.ConnectionString;
    }

    // Builds the services exactly like the app does (same Infrastructure registration).
    public static ServiceProvider BuildServices(string connectionString)
    {
        var services = new ServiceCollection();
        InfrastructureServices.AddInfrastructure(services, connectionString);
        return services.BuildServiceProvider();
    }

    // Called before every test: same starting data every time.
    public static void Reset(IDbContextFactory<EquipmentBorrowingDbContext> contextFactory)
    {
        lock (_lock)
        {
            using EquipmentBorrowingDbContext context = contextFactory.CreateDbContext();

            // Only once per test run: drop the test database and build it again from the migrations.
            if (!_schemaCreated)
            {
                context.Database.EnsureDeleted();
                context.Database.Migrate();
                _schemaCreated = true;
            }

            // Empty all tables and restart the Ids at 1, then add the sample data again.
            context.Database.ExecuteSqlRaw(
                "TRUNCATE \"Borrowings\", \"Students\", \"Equipment\" RESTART IDENTITY CASCADE;");

            DatabaseInitializer.SeedSampleData(context);
        }
    }
}

// Base class for all tests. A new instance is created for every test,
// so every test starts with a freshly reset database.
public abstract class DatabaseTestBase : IDisposable
{
    protected readonly ServiceProvider Provider;
    protected readonly IDbContextFactory<EquipmentBorrowingDbContext> ContextFactory;
    protected readonly IUnitOfWorkFactory UnitOfWorkFactory;
    protected readonly IDatabaseErrorClassifier ErrorClassifier;
    protected readonly CheckoutFailureSimulator FailureSimulator = new CheckoutFailureSimulator();

    protected readonly BorrowEquipmentService BorrowService;
    protected readonly ReturnEquipmentService ReturnService;
    protected readonly BorrowingQueryService BorrowingQueries;
    protected readonly StudentManagementService StudentManagement;
    protected readonly EquipmentManagementService EquipmentManagement;

    protected readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    protected DatabaseTestBase()
    {
        Provider = TestDatabase.BuildServices(TestDatabase.GetConnectionString());
        ContextFactory = Provider.GetRequiredService<IDbContextFactory<EquipmentBorrowingDbContext>>();
        UnitOfWorkFactory = Provider.GetRequiredService<IUnitOfWorkFactory>();
        ErrorClassifier = Provider.GetRequiredService<IDatabaseErrorClassifier>();

        TestDatabase.Reset(ContextFactory);

        BorrowService = new BorrowEquipmentService(UnitOfWorkFactory, ErrorClassifier, FailureSimulator);
        ReturnService = new ReturnEquipmentService(UnitOfWorkFactory, ErrorClassifier);
        BorrowingQueries = new BorrowingQueryService(UnitOfWorkFactory, ErrorClassifier);
        StudentManagement = new StudentManagementService(UnitOfWorkFactory, ErrorClassifier);
        EquipmentManagement = new EquipmentManagementService(UnitOfWorkFactory, ErrorClassifier);
    }

    // A BRAND NEW DbContext for checking what is really stored in PostgreSQL.
    // (Required by the lab: verification reads must not reuse the context that wrote the data.)
    protected EquipmentBorrowingDbContext FreshContext()
    {
        return ContextFactory.CreateDbContext();
    }

    protected int StudentId(string studentNumber)
    {
        using EquipmentBorrowingDbContext context = FreshContext();
        return context.Students.Single(s => s.StudentNumber == studentNumber).Id;
    }

    protected int EquipmentId(string equipmentCode)
    {
        using EquipmentBorrowingDbContext context = FreshContext();
        return context.Equipment.Single(e => e.EquipmentCode == equipmentCode).Id;
    }

    protected int CountBorrowings()
    {
        using EquipmentBorrowingDbContext context = FreshContext();
        return context.Borrowings.Count();
    }

    protected int CountActiveBorrowings(int studentId)
    {
        using EquipmentBorrowingDbContext context = FreshContext();
        return context.Borrowings.Count(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active);
    }

    protected bool IsAvailable(string equipmentCode)
    {
        using EquipmentBorrowingDbContext context = FreshContext();
        return context.Equipment.Single(e => e.EquipmentCode == equipmentCode).IsAvailable;
    }

    public void Dispose()
    {
        Provider.Dispose();
    }
}
