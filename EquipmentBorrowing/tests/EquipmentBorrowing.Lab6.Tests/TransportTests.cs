using System.Text.Json;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.Services;
using EquipmentBorrowing.Lab6.Tests.Support;

namespace EquipmentBorrowing.Lab6.Tests;

// TC6-E07 Transport and uncertain outcome.
// These tests use the REAL desktop client class (DesktopApi) and check how it reacts to:
//   a) an API that cannot be reached at all,
//   b) a reachable API that answers with a safe 500,
//   c) a commit whose acknowledgement is lost (outcome unknown).

// A pretend network that always fails, like an API that is switched off.
// It counts how many times the client tried to send something.
public class UnreachableHandler : HttpMessageHandler
{
    public int RequestCount;
    public int ResponseCount;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        throw new HttpRequestException("Connection refused (simulated for TC6-E07a).");
    }
}

// Sits between the desktop client and the real API and writes down what happened.
public class RecordingHandler : DelegatingHandler
{
    public int RequestCount;
    public int PostCount;
    public int LastStatus;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;

        if (request.Method == HttpMethod.Post)
        {
            PostCount++;
        }

        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);
        LastStatus = (int)response.StatusCode;
        return response;
    }
}

public class TransportTests : ApiTestBase
{
    [Fact]
    public async Task TC6_E07a_Unreachable_API_gives_a_transport_failure_with_no_HTTP_response_and_no_retry()
    {
        string before = LabDatabase.Snapshot();

        UnreachableHandler handler = new UnreachableHandler();
        HttpClient http = new HttpClient(handler);
        http.BaseAddress = new Uri("http://localhost/");
        DesktopApi desktop = new DesktopApi(http);

        // A write: the client cannot know whether the server saw it, so it says "unknown".
        OperationResult write = await desktop.CheckOutMultipleAsync(1, new List<int> { 2, 6 }, LabDatabase.D.AddDays(2));

        Assert.Equal(ResultKind.UnknownOutcome, write.Kind);
        Assert.Contains("Cannot reach the API", write.Message);
        Assert.DoesNotContain("500", write.Message);

        // Exactly one attempt: no automatic retry. And no HTTP response was ever received.
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(0, handler.ResponseCount);

        // A read fails with the same kind of message.
        QueryResult<EquipmentBorrowing.Domain.Student> read = await desktop.GetAllStudentsAsync();
        Assert.False(read.IsSuccess);
        Assert.Contains("Cannot reach the API", read.ErrorMessage);
        Assert.Equal(2, handler.RequestCount);

        // The real database was never touched.
        Assert.Equal(before, LabDatabase.Snapshot());
    }

    [Fact]
    public async Task TC6_E07b_A_reachable_API_error_is_a_received_500_with_safe_feedback_and_no_partial_writes()
    {
        int s4 = await StudentIdAsync("ST-400");
        int e2 = await EquipmentIdAsync("EQ-002");
        int e6 = await EquipmentIdAsync("EQ-006");
        string before = LabDatabase.Snapshot();

        // Controlled, reachable server-side failure.
        Factory.FailureSimulator.Mode = SimulatedCheckoutFailure.AfterFirstWrite;

        RecordingHandler recorder = new RecordingHandler();
        HttpClient http = Factory.CreateDefaultClient(recorder);
        DesktopApi desktop = new DesktopApi(http);

        OperationResult result = await desktop.CheckOutMultipleAsync(s4, new List<int> { e2, e6 }, LabDatabase.D.AddDays(2));

        // A response WAS received and it was a 500.
        Assert.Equal(500, recorder.LastStatus);

        // The client treats it as a normal failure, NOT as "cannot reach the API" and NOT as unknown.
        Assert.Equal(ResultKind.Failed, result.Kind);
        Assert.Contains("The API responded with an error", result.Message);
        Assert.DoesNotContain("Cannot reach", result.Message);
        Assert.DoesNotContain("Simulated", result.Message);
        Assert.DoesNotContain("Npgsql", result.Message);

        Assert.Equal(1, recorder.PostCount);

        // Known failure means no partial writes.
        Assert.Equal(before, LabDatabase.Snapshot());
        Assert.True(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);
        Assert.True(LabDatabase.FindEquipment("EQ-006")!.IsAvailable);
    }

    [Fact]
    public async Task TC6_E07c_Lost_commit_acknowledgement_is_reported_unknown_is_not_retried_and_PostgreSQL_shows_the_truth()
    {
        int s4 = await StudentIdAsync("ST-400");
        int e2 = await EquipmentIdAsync("EQ-002");
        int e6 = await EquipmentIdAsync("EQ-006");

        // The commit really happens, but the answer is "lost".
        Factory.FailureSimulator.Mode = SimulatedCheckoutFailure.LostCommitAcknowledgement;

        RecordingHandler recorder = new RecordingHandler();
        HttpClient http = Factory.CreateDefaultClient(recorder);
        DesktopApi desktop = new DesktopApi(http);

        OperationResult result = await desktop.CheckOutMultipleAsync(s4, new List<int> { e2, e6 }, LabDatabase.D.AddDays(2));

        // The client says the outcome is UNKNOWN (not success, not "nothing saved").
        Assert.Equal(500, recorder.LastStatus);
        Assert.Equal(ResultKind.UnknownOutcome, result.Kind);
        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("Nothing was saved", result.Message);

        // No automatic retry: only ONE write request was sent.
        Assert.Equal(1, recorder.PostCount);

        // Only now check the fresh PostgreSQL state (this is what a user would do before acting).
        // Because the commit really happened, the result is ALL saved: both borrowings and both items.
        Assert.Equal(2, LabDatabase.CountActiveBorrowings(s4));
        Assert.Equal(1, LabDatabase.CountActiveForEquipment(e2));
        Assert.Equal(1, LabDatabase.CountActiveForEquipment(e6));
        Assert.False(LabDatabase.FindEquipment("EQ-002")!.IsAvailable);
        Assert.False(LabDatabase.FindEquipment("EQ-006")!.IsAvailable);
        Assert.Equal(6, LabDatabase.CountBorrowings());

        // Checking the list is a read (GET), not another write.
        var active = await desktop.GetActiveBorrowingsAsync();
        Assert.True(active.IsSuccess);
        Assert.Equal(5, active.Items.Count);
        Assert.Equal(1, recorder.PostCount);
    }
}
