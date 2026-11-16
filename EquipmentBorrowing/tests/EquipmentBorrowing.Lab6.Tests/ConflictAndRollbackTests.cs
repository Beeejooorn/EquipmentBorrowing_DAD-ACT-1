using System.Text.Json;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Lab6.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Lab6.Tests;

// TC6-E04 PostgreSQL rollback (a fault AFTER a real write must leave nothing behind)
// TC6-E05 Stale and conflicting state
public class ConflictAndRollbackTests : ApiTestBase
{
    // ---------------- TC6-E04 ----------------

    [Fact]
    public async Task TC6_E04_Fault_after_the_first_write_rolls_everything_back_in_PostgreSQL()
    {
        int s4 = await StudentIdAsync("ST-400");
        int e2 = await EquipmentIdAsync("EQ-002");
        int e6 = await EquipmentIdAsync("EQ-006");

        // Baseline, read on a fresh connection.
        string baseline = LabDatabase.Snapshot();
        Assert.True(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);
        Assert.True(LabDatabase.FindEquipment("EQ-006")!.IsAvailable);

        // Turn on the controlled fault: it fires after the FIRST item was really written
        // (INSERT + UPDATE sent to PostgreSQL) but before the commit.
        Factory.FailureSimulator.Mode = SimulatedCheckoutFailure.AfterFirstWrite;

        // Step A: call the service directly so we can read its own message.
        // The message is only produced by the fault code AFTER the first write, so it proves
        // the fault was reached after a real database write.
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            BorrowEquipmentService service = scope.ServiceProvider.GetRequiredService<BorrowEquipmentService>();

            OperationResult direct = await service.CheckOutMultipleAsync(
                s4,
                new List<int> { e2, e6 },
                LabDatabase.D.AddDays(2));

            Assert.False(direct.IsSuccess);
            Assert.Equal(ResultKind.Failed, direct.Kind);
            Assert.Contains("was written inside the transaction", direct.Message);
            Assert.Contains("rolled back", direct.Message);
        }

        // The context was disposed. A brand new connection must see the baseline again.
        Assert.Equal(baseline, LabDatabase.Snapshot());

        // Step B: the same request through real HTTP gives a safe 500.
        ApiResponse http = await PostAsync("/api/v1/checkout-requests", new
        {
            studentId = s4,
            equipmentIds = new int[] { e2, e6 },
            expectedReturnDate = Day(2)
        });

        Assert.Equal(500, http.Status);
        Assert.Equal("server_error", http.Json().GetProperty("code").GetString());
        AssertSafeErrorBody(http);
        Assert.DoesNotContain("Simulated", http.Body);

        // Fresh independent state is identical to the baseline.
        Assert.Equal(baseline, LabDatabase.Snapshot());
        Assert.Equal(4, LabDatabase.CountBorrowings());
        Assert.Equal(0, LabDatabase.CountActiveBorrowings(s4));
        Assert.True(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);
        Assert.True(LabDatabase.FindEquipment("EQ-006")!.IsAvailable);
    }

    // ---------------- TC6-E05 ----------------

    [Fact]
    public async Task TC6_E05_Stale_requests_get_409_and_PostgreSQL_refuses_a_second_active_row()
    {
        int s1 = await StudentIdAsync("ST-100");
        int s3 = await StudentIdAsync("ST-300");
        int s4 = await StudentIdAsync("ST-400");
        int e2 = await EquipmentIdAsync("EQ-002");
        int e6 = await EquipmentIdAsync("EQ-006");

        // 1. Observe: E2 is available (this is the "stale" view another user still has).
        ApiResponse observed = await GetAsync("/api/v1/equipment?available=true");
        Assert.Equal(200, observed.Status);
        Assert.Contains("EQ-002", BorrowedCodesOf(observed.Json()));

        // 2. Another user issues E2 first: 201.
        ApiResponse first = await PostAsync("/api/v1/borrowings", new
        {
            studentId = s4,
            equipmentId = e2,
            expectedReturnDate = Day(2)
        });
        Assert.Equal(201, first.Status);

        string afterFirst = LabDatabase.Snapshot();

        // 3. The stale single request for the same item: 409.
        ApiResponse staleSingle = await PostAsync("/api/v1/borrowings", new
        {
            studentId = s3,
            equipmentId = e2,
            expectedReturnDate = Day(2)
        });
        Assert.Equal(409, staleSingle.Status);
        Assert.Equal("borrowing_conflict", staleSingle.Json().GetProperty("code").GetString());

        // 4. A stale multi-item checkout that includes E2: 409 and E6 is untouched.
        ApiResponse staleCheckout = await PostAsync("/api/v1/checkout-requests", new
        {
            studentId = s1,
            equipmentIds = new int[] { e2, e6 },
            expectedReturnDate = Day(2)
        });
        Assert.Equal(409, staleCheckout.Status);
        Assert.Equal("checkout_conflict", staleCheckout.Json().GetProperty("code").GetString());

        // Exactly one active record for E2, nothing else changed after the first success.
        Assert.Equal(1, LabDatabase.CountActiveForEquipment(e2));
        Assert.True(LabDatabase.FindEquipment("EQ-006")!.IsAvailable);
        Assert.Equal(afterFirst, LabDatabase.Snapshot());

        // 5. Go around the API: ask PostgreSQL directly for a second ACTIVE row for E2.
        var rejected = LabDatabase.TryRawSql(
            "INSERT INTO \"Borrowings\" (\"DateBorrowed\", \"EquipmentId\", \"ExpectedReturnDate\", \"ReturnedAt\", \"Status\", \"StudentId\") " +
            "VALUES (CURRENT_DATE, " + e2 + ", CURRENT_DATE + 2, NULL, 'Active', " + s3 + ")");

        Assert.NotNull(rejected);
        Assert.Equal(LabDatabase.UniqueViolation, rejected!.SqlState);

        // The rejected insert left no trace.
        Assert.Equal(1, LabDatabase.CountActiveForEquipment(e2));
        Assert.Equal(afterFirst, LabDatabase.Snapshot());
    }
}
