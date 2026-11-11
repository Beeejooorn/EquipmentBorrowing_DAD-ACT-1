using EquipmentBorrowing.Domain;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Persistence;

// Runs once when the application starts:
//   1. applies any migrations that are not applied yet (creates the schema on a clean database)
//   2. adds sample data, but ONLY if the database has no students and no equipment yet.
// An existing database is never overwritten.
public class DatabaseInitializer
{
    private readonly IDbContextFactory<EquipmentBorrowingDbContext> _contextFactory;

    public DatabaseInitializer(IDbContextFactory<EquipmentBorrowingDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    // This uses the normal (non-async) EF methods on purpose: it runs at startup
    // before the main window is shown, so there is no screen to keep responsive yet.
    public void Initialize()
    {
        using EquipmentBorrowingDbContext context = _contextFactory.CreateDbContext();

        context.Database.Migrate();

        bool alreadyHasData = context.Students.Any() || context.Equipment.Any();

        if (alreadyHasData)
        {
            return;
        }

        SeedSampleData(context);
    }

    // Sample data that covers the Lab 4 test situations (Appendix C):
    // several students and items, active and returned history, a restricted student,
    // available and unavailable items, and a student and items with no history at all.
    public static void SeedSampleData(EquipmentBorrowingDbContext context)
    {
        using var transaction = context.Database.BeginTransaction();

        Student ana = new Student(0, "2023-0001", "Ana Reyes", "ana.reyes@campus.edu", true);
        Student ben = new Student(0, "2023-0002", "Ben Cruz", "0917-555-0102", true);
        Student carla = new Student(0, "2023-0003", "Carla Santos", null, false); // restricted
        Student dan = new Student(0, "2023-0004", "Dan Lim", null, true);         // no history

        context.Students.Add(ana);
        context.Students.Add(ben);
        context.Students.Add(carla);
        context.Students.Add(dan);

        Equipment laptop1 = new Equipment(0, "EQ-LAP-01", "Laptop 01", "Dell Latitude 5440", false);
        Equipment laptop2 = new Equipment(0, "EQ-LAP-02", "Laptop 02", "Dell Latitude 5440", true);
        Equipment projector = new Equipment(0, "EQ-PRJ-01", "Projector 01", "Epson EB-X51", true);
        Equipment camera = new Equipment(0, "EQ-CAM-01", "Camera 01", "Canon EOS M50", false);
        Equipment tablet = new Equipment(0, "EQ-TAB-01", "Tablet 01", "iPad 10th gen", true);
        Equipment microphone = new Equipment(0, "EQ-MIC-01", "Microphone 01", null, true);

        context.Equipment.Add(laptop1);
        context.Equipment.Add(laptop2);
        context.Equipment.Add(projector);
        context.Equipment.Add(camera);
        context.Equipment.Add(tablet);
        context.Equipment.Add(microphone);

        // Save first so the students and items get their Ids.
        context.SaveChanges();

        DateOnly today = DateOnly.FromDateTime(DateTime.Now);

        // Ana: one returned (Laptop 01) and one active (Camera 01).
        Borrowing anaReturned = new Borrowing(0, ana.Id, laptop1.Id, today.AddDays(-20), today.AddDays(-13));
        anaReturned.MarkAsReturned(today.AddDays(-15));

        Borrowing anaActive = new Borrowing(0, ana.Id, camera.Id, today.AddDays(-3), today.AddDays(4));

        // Ben: one returned (Projector 01) and one active (Laptop 01).
        Borrowing benReturned = new Borrowing(0, ben.Id, projector.Id, today.AddDays(-12), today.AddDays(-5));
        benReturned.MarkAsReturned(today.AddDays(-6));

        Borrowing benActive = new Borrowing(0, ben.Id, laptop1.Id, today.AddDays(-2), today.AddDays(5));

        // Carla (now restricted) returned the tablet earlier.
        Borrowing carlaReturned = new Borrowing(0, carla.Id, tablet.Id, today.AddDays(-30), today.AddDays(-23));
        carlaReturned.MarkAsReturned(today.AddDays(-24));

        context.Borrowings.Add(anaReturned);
        context.Borrowings.Add(anaActive);
        context.Borrowings.Add(benReturned);
        context.Borrowings.Add(benActive);
        context.Borrowings.Add(carlaReturned);

        context.SaveChanges();
        transaction.Commit();
    }
}
