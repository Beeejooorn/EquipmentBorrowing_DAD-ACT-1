using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Tests;

// Appendix C - A18 (BR-18, BR-19): when PostgreSQL cannot be reached, the services
// return a clear message instead of throwing, and nothing is claimed to be saved.
// Port 1 on this computer has no database server, so every connection attempt fails.
public class ConnectionFailureTests : IDisposable
{
    private const string UnreachableDatabase =
        "Host=localhost;Port=1;Database=equipmentborrowing;Username=postgres;Password=not-used;Timeout=3";

    private readonly ServiceProvider _provider;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IDatabaseErrorClassifier _errorClassifier;

    public ConnectionFailureTests()
    {
        _provider = TestDatabase.BuildServices(UnreachableDatabase);
        _unitOfWorkFactory = _provider.GetRequiredService<IUnitOfWorkFactory>();
        _errorClassifier = _provider.GetRequiredService<IDatabaseErrorClassifier>();
    }

    [Fact]
    public async Task LoadingAList_WhenDatabaseIsDown_ReturnsAnErrorResultNotAnException()
    {
        EquipmentQueryService service = new EquipmentQueryService(_unitOfWorkFactory, _errorClassifier);

        QueryResult<Equipment> result = await service.GetAllEquipmentAsync();

        Assert.False(result.IsSuccess);
        Assert.False(result.IsNotFound); // a connection failure is NOT the same as "not found"
        Assert.Contains("cannot be reached", result.ErrorMessage);
    }

    [Fact]
    public async Task Saving_WhenDatabaseIsDown_ReportsFailureAndThatNothingWasSaved()
    {
        BorrowEquipmentService service = new BorrowEquipmentService(_unitOfWorkFactory, _errorClassifier, new CheckoutFailureSimulator());

        OperationResult result = await service.BorrowAsync(1, 2, DateOnly.FromDateTime(DateTime.Now).AddDays(7));

        Assert.Equal(ResultKind.Failed, result.Kind);
        Assert.Contains("Nothing was saved", result.Message);
    }

    [Fact]
    public void Classifier_TreatsANetworkErrorAsConnectionFailure()
    {
        DatabaseErrorClassifier classifier = new DatabaseErrorClassifier();

        Exception wrapped = new InvalidOperationException("outer", new System.Net.Sockets.SocketException());

        Assert.Equal(DatabaseErrorKind.ConnectionFailed, classifier.Classify(wrapped));
    }

    [Fact]
    public void ErrorMessage_ForConnectionLostDuringCommit_SaysTheOutcomeIsUnknown()
    {
        OperationResult result = DatabaseErrors.ToResult(DatabaseErrorKind.ConnectionFailed, commitStarted: true);

        Assert.Equal(ResultKind.UnknownOutcome, result.Kind);
        Assert.Contains("UNKNOWN", result.Message);
        Assert.DoesNotContain("Nothing was saved", result.Message);
    }

    public void Dispose()
    {
        _provider.Dispose();
    }
}
