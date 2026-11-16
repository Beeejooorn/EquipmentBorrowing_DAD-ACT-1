using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Lab6.Tests.Support;

namespace EquipmentBorrowing.Lab6.Tests;

// These tests check the TEST SETUP itself (not an application feature):
//   - the PostgreSQL fixtures S1-S4 / E1-E7 are exactly the ones in the Lab 6 suite,
//   - the real API starts and answers,
//   - every test starts from a clean database (no test depends on another test).
public class FixtureAndIsolationTests : ApiTestBase
{
    [Fact]
    public void Fixtures_match_the_lab6_suite()
    {
        Assert.Equal(4, LabDatabase.CountStudents());
        Assert.Equal(7, LabDatabase.CountEquipment());
        Assert.Equal(4, LabDatabase.CountBorrowings());

        // Students: S2 is the only restricted one.
        Assert.True(LabDatabase.FindStudent("ST-100")!.IsAllowedToBorrow);
        Assert.False(LabDatabase.FindStudent("ST-200")!.IsAllowedToBorrow);
        Assert.True(LabDatabase.FindStudent("ST-300")!.IsAllowedToBorrow);
        Assert.True(LabDatabase.FindStudent("ST-400")!.IsAllowedToBorrow);

        // Equipment: E1, E4 and E5 are unavailable, the others are available.
        Assert.False(LabDatabase.FindEquipment("EQ-001")!.IsAvailable);
        Assert.True(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);
        Assert.True(LabDatabase.FindEquipment("EQ-003")!.IsAvailable);
        Assert.False(LabDatabase.FindEquipment("EQ-004")!.IsAvailable);
        Assert.False(LabDatabase.FindEquipment("EQ-005")!.IsAvailable);
        Assert.True(LabDatabase.FindEquipment("EQ-006")!.IsAvailable);
        Assert.True(LabDatabase.FindEquipment("EQ-007")!.IsAvailable);
    }

    [Fact]
    public void Fixture_borrowings_use_dates_relative_to_D()
    {
        List<Borrowing> borrowings = LabDatabase.AllBorrowings();
        Assert.Equal(4, borrowings.Count);

        // Borrowing 1: S1 has E1, active, borrowed D-9, expected D-1.
        Assert.Equal(BorrowingStatus.Active, borrowings[0].Status);
        Assert.Equal(D.AddDays(-9), borrowings[0].DateBorrowed);
        Assert.Equal(D.AddDays(-1), borrowings[0].ExpectedReturnDate);

        // Borrowing 2: S1 had E3, returned D-2, borrowed D-8, expected D+2.
        Assert.Equal(BorrowingStatus.Returned, borrowings[1].Status);
        Assert.Equal(D.AddDays(-8), borrowings[1].DateBorrowed);
        Assert.Equal(D.AddDays(2), borrowings[1].ExpectedReturnDate);
        Assert.Equal(D.AddDays(-2), borrowings[1].ReturnedAt);

        // Borrowings 3 and 4: S3 has E4 and E5, active, borrowed D-7, expected D+2.
        Assert.Equal(BorrowingStatus.Active, borrowings[2].Status);
        Assert.Equal(BorrowingStatus.Active, borrowings[3].Status);
        Assert.Equal(D.AddDays(-7), borrowings[2].DateBorrowed);
        Assert.Equal(D.AddDays(2), borrowings[3].ExpectedReturnDate);
    }

    [Fact]
    public async Task Real_api_starts_and_answers_through_the_http_pipeline()
    {
        ApiResponse root = await GetAsync("/");
        Assert.Equal(200, root.Status);

        // Ids are resolved through the API, as the suite asks.
        int studentId = await StudentIdAsync("ST-100");
        int equipmentId = await EquipmentIdAsync("EQ-001");

        Assert.Equal(LabDatabase.FindStudent("ST-100")!.Id, studentId);
        Assert.Equal(LabDatabase.FindEquipment("EQ-001")!.Id, equipmentId);
    }

    // The next two tests prove isolation. Whatever order xUnit runs them in,
    // the second check always sees 4 students, because every test resets the database first.
    [Fact]
    public async Task Isolation_test_A_changes_data()
    {
        ApiResponse created = await PostAsync("/api/v1/students", new
        {
            studentNumber = "ST-900",
            fullName = "Isolation Tester"
        });

        Assert.Equal(201, created.Status);
        Assert.Equal(5, LabDatabase.CountStudents());
    }

    [Fact]
    public void Isolation_test_B_still_sees_clean_fixtures()
    {
        Assert.Equal(4, LabDatabase.CountStudents());
        Assert.Null(LabDatabase.FindStudent("ST-900"));
    }
}
