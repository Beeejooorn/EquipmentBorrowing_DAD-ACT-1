namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

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

    // UC-07 View Student Borrowing History (active and returned, only this student).
    public async Task<QueryResult<BorrowingRecord>> GetStudentHistoryAsync(int studentId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();

            // A missing student is different from a student with no history (BR-19).
            Student? student = await unitOfWork.Students.GetByIdAsync(studentId, cancellationToken);

            if (student is null)
            {
                return QueryResult<BorrowingRecord>.NotFound("This student no longer exists. Refresh the student list.");
            }

            List<BorrowingRecord> records = await unitOfWork.Borrowings.GetStudentHistoryAsync(studentId, cancellationToken);
            return QueryResult<BorrowingRecord>.Ok(records);
        }
        catch (Exception ex)
        {
            return QueryResult<BorrowingRecord>.Error(DatabaseErrors.ToLoadMessage(_errorClassifier.Classify(ex)));
        }
    }

    // UC-08 View Equipment Borrowing History (active and returned, only this item).
    public async Task<QueryResult<BorrowingRecord>> GetEquipmentHistoryAsync(int equipmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();

            Equipment? equipment = await unitOfWork.Equipment.GetByIdAsync(equipmentId, cancellationToken);

            if (equipment is null)
            {
                return QueryResult<BorrowingRecord>.NotFound("This equipment no longer exists. Refresh the equipment list.");
            }

            List<BorrowingRecord> records = await unitOfWork.Borrowings.GetEquipmentHistoryAsync(equipmentId, cancellationToken);
            return QueryResult<BorrowingRecord>.Ok(records);
        }
        catch (Exception ex)
        {
            return QueryResult<BorrowingRecord>.Error(DatabaseErrors.ToLoadMessage(_errorClassifier.Classify(ex)));
        }
    }
}
