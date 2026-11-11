namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;

// Read-only borrowing queries.
public class BorrowingQueryService
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IDatabaseErrorClassifier _errorClassifier;

    public BorrowingQueryService(IUnitOfWorkFactory unitOfWorkFactory, IDatabaseErrorClassifier errorClassifier)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _errorClassifier = errorClassifier;
    }

    // UC-04 View Active Borrowings
    public async Task<QueryResult<BorrowingRecord>> GetActiveBorrowingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            List<BorrowingRecord> records = await unitOfWork.Borrowings.GetActiveRecordsAsync(cancellationToken);
            return QueryResult<BorrowingRecord>.Ok(records);
        }
        catch (Exception ex)
        {
            return QueryResult<BorrowingRecord>.Error(DatabaseErrors.ToLoadMessage(_errorClassifier.Classify(ex)));
        }
    }
}
