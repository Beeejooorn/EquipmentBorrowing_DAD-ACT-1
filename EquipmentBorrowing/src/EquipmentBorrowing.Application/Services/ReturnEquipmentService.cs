namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

// UC-05 Return Equipment.
public class ReturnEquipmentService
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IDatabaseErrorClassifier _errorClassifier;

    public ReturnEquipmentService(IUnitOfWorkFactory unitOfWorkFactory, IDatabaseErrorClassifier errorClassifier)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _errorClassifier = errorClassifier;
    }

    // We receive only the Id of the borrowing the user selected.
    // The record is read again from the database, so an old (stale) screen
    // can never return something that was already returned.
    public async Task<OperationResult> ReturnAsync(
        int borrowingId,
        CancellationToken cancellationToken = default)
    {
        bool commitStarted = false;

        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            await unitOfWork.BeginTransactionAsync(cancellationToken);

            Borrowing? borrowing = await unitOfWork.Borrowings.GetByIdAsync(borrowingId, cancellationToken);

            if (borrowing is null)
            {
                return OperationResult.NotFound("This borrowing record no longer exists. The list has been refreshed.");
            }

            // BR-13: only an active borrowing can be returned. A repeat return changes nothing.
            if (borrowing.Status == BorrowingStatus.Returned)
            {
                string returnedDate = "an earlier date";

                if (borrowing.ReturnedAt.HasValue)
                {
                    returnedDate = borrowing.ReturnedAt.Value.ToString("yyyy-MM-dd");
                }

                return OperationResult.Rejected(
                    $"Borrowing #{borrowing.Id} was already returned on {returnedDate}. Nothing was changed.");
            }

            // The return date is today's local calendar date and cannot be before checkout.
            // Returning early (before the expected date) is allowed.
            DateOnly today = DateOnly.FromDateTime(DateTime.Now);

            if (today < borrowing.DateBorrowed)
            {
                return OperationResult.Rejected("The return date cannot be earlier than the checkout date. Nothing was changed.");
            }

            Equipment? equipment = await unitOfWork.Equipment.GetByIdAsync(borrowing.EquipmentId, cancellationToken);

            if (equipment is null)
            {
                return OperationResult.NotFound("The equipment of this borrowing no longer exists. Nothing was changed.");
            }

            // Both changes are saved in the same transaction.
            // Note: student eligibility is NOT checked, a restricted student can still return items (BR-04).
            borrowing.MarkAsReturned(today);
            equipment.MarkAsAvailable();

            await unitOfWork.SaveChangesAsync(cancellationToken);

            commitStarted = true;
            await unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success(
                $"{equipment.Name} ({equipment.EquipmentCode}) was returned on {today:yyyy-MM-dd} and is available again.");
        }
        catch (Exception ex)
        {
            DatabaseErrorKind kind = _errorClassifier.Classify(ex);
            return DatabaseErrors.ToResult(kind, commitStarted);
        }
    }
}
