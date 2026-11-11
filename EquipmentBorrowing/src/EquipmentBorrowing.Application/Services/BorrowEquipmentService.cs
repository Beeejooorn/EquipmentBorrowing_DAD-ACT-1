namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

// UC-03 Borrow Equipment and UC-06 Multi-Item Checkout.
// Both use the same steps (IssueAsync), the checkout just has a few extra input checks.
public class BorrowEquipmentService
{
    private const int MaxActiveBorrowings = 3;

    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IDatabaseErrorClassifier _errorClassifier;
    private readonly CheckoutFailureSimulator _failureSimulator;

    public BorrowEquipmentService(
        IUnitOfWorkFactory unitOfWorkFactory,
        IDatabaseErrorClassifier errorClassifier,
        CheckoutFailureSimulator failureSimulator)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _errorClassifier = errorClassifier;
        _failureSimulator = failureSimulator;
    }

    // UC-03: one item for one student.
    public Task<OperationResult> BorrowAsync(
        int studentId,
        int equipmentId,
        DateOnly? expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        List<int> equipmentIds = new List<int>();
        equipmentIds.Add(equipmentId);

        return IssueAsync(studentId, equipmentIds, expectedReturnDate, false, cancellationToken);
    }

    // UC-06: two or more different items for one student, all saved or none saved (BR-12).
    public Task<OperationResult> CheckOutMultipleAsync(
        int studentId,
        List<int> equipmentIds,
        DateOnly? expectedReturnDate,
        CancellationToken cancellationToken = default)
    {
        return IssueAsync(studentId, equipmentIds, expectedReturnDate, true, cancellationToken);
    }

    private async Task<OperationResult> IssueAsync(
        int studentId,
        List<int> equipmentIds,
        DateOnly? expectedReturnDate,
        bool isMultiItemCheckout,
        CancellationToken cancellationToken)
    {
        // ---- Step 1: checks that do not need the database ----

        if (isMultiItemCheckout)
        {
            if (equipmentIds.Count < 2)
            {
                return OperationResult.Rejected(
                    "A multi-item checkout needs at least two different items. " +
                    "To borrow a single item, use Borrow Equipment on the Equipment Catalog screen.");
            }

            // BR-12: duplicates are rejected, never silently removed.
            List<int> seenIds = new List<int>();

            foreach (int equipmentId in equipmentIds)
            {
                if (seenIds.Contains(equipmentId))
                {
                    return OperationResult.Rejected(
                        "The same item was added to the checkout more than once. " +
                        "Remove the duplicate and try again. Nothing was saved.");
                }

                seenIds.Add(equipmentId);
            }
        }

        if (expectedReturnDate is null)
        {
            return OperationResult.Rejected("Please choose an expected return date.");
        }

        // The checkout date is today's local calendar date (BR-11).
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);

        if (expectedReturnDate.Value <= today)
        {
            return OperationResult.Rejected(
                $"The expected return date must be later than the checkout date ({today:yyyy-MM-dd}).");
        }

        // ---- Step 2: revalidate everything inside one transaction (BR-15) ----

        bool commitStarted = false;

        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            await unitOfWork.BeginTransactionAsync(cancellationToken);

            Student? student = await unitOfWork.Students.GetByIdAsync(studentId, cancellationToken);

            if (student is null)
            {
                return OperationResult.NotFound("The selected student no longer exists. The list has been refreshed.");
            }

            if (!student.IsAllowedToBorrow)
            {
                return OperationResult.Rejected($"{student.Name} is restricted and cannot borrow equipment. Nothing was saved.");
            }

            // BR-09: active borrowings + requested items must not go over the limit.
            int activeCount = await unitOfWork.Borrowings.CountActiveByStudentAsync(studentId, cancellationToken);

            if (activeCount + equipmentIds.Count > MaxActiveBorrowings)
            {
                return OperationResult.Rejected(
                    $"{student.Name} already has {activeCount} active borrowing(s). " +
                    $"Borrowing {equipmentIds.Count} more would go over the limit of {MaxActiveBorrowings}. Nothing was saved.");
            }

            List<Equipment> itemsToIssue = new List<Equipment>();

            foreach (int equipmentId in equipmentIds)
            {
                Equipment? equipment = await unitOfWork.Equipment.GetByIdAsync(equipmentId, cancellationToken);

                if (equipment is null)
                {
                    return OperationResult.NotFound("A selected item no longer exists. The list has been refreshed. Nothing was saved.");
                }

                // BR-10: must be available AND have no active borrowing.
                bool hasActiveBorrowing = await unitOfWork.Borrowings.HasActiveBorrowingAsync(equipmentId, cancellationToken);

                if (!equipment.IsAvailable || hasActiveBorrowing)
                {
                    return OperationResult.Rejected(
                        $"{equipment.Name} ({equipment.EquipmentCode}) is not available. Nothing was saved.");
                }

                itemsToIssue.Add(equipment);
            }

            // ---- Step 3: write one borrowing per item and mark each item unavailable ----

            int itemsWritten = 0;

            foreach (Equipment equipment in itemsToIssue)
            {
                Borrowing borrowing = new Borrowing(
                    Id: 0,
                    StudentId: studentId,
                    EquipmentId: equipment.Id,
                    DateBorrowed: today,
                    ExpectedReturnDate: expectedReturnDate.Value);

                await unitOfWork.Borrowings.AddAsync(borrowing, cancellationToken);
                equipment.MarkAsUnAvailable();

                // Sends the INSERT and UPDATE to PostgreSQL, but they are not permanent yet.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                itemsWritten++;

                // TEST SEAM: fail after a real write happened inside the transaction.
                if (isMultiItemCheckout
                    && itemsWritten == 1
                    && _failureSimulator.Mode == SimulatedCheckoutFailure.AfterFirstWrite)
                {
                    throw new SimulatedFailureException(
                        $"Simulated failure after {equipment.Name} was written inside the transaction.");
                }
            }

            // ---- Step 4: commit, everything becomes permanent together ----

            commitStarted = true;
            await unitOfWork.CommitAsync(cancellationToken);

            // TEST SEAM: the commit succeeded, but we pretend the answer never arrived.
            if (isMultiItemCheckout && _failureSimulator.Mode == SimulatedCheckoutFailure.LostCommitAcknowledgement)
            {
                throw new SimulatedConnectionLostException("Simulated connection loss after the commit was sent.");
            }

            return OperationResult.Success(
                $"{BuildItemList(itemsToIssue)} issued to {student.Name}. " +
                $"Expected return: {expectedReturnDate.Value:yyyy-MM-dd}.");
        }
        catch (SimulatedFailureException ex)
        {
            // Leaving the "await using" block already rolled the transaction back.
            return new OperationResult(ResultKind.Failed,
                $"{ex.Message} The whole transaction was rolled back, so NOTHING was saved (no borrowings, no availability changes).");
        }
        catch (SimulatedConnectionLostException)
        {
            // Same handling as a real lost connection during commit (BR-18).
            return DatabaseErrors.ToResult(DatabaseErrorKind.ConnectionFailed, commitStarted);
        }
        catch (Exception ex)
        {
            // Leaving the "await using" block above already rolled the transaction back
            // (unless it was committed), so a known failure leaves no partial changes.
            DatabaseErrorKind kind = _errorClassifier.Classify(ex);
            return DatabaseErrors.ToResult(kind, commitStarted);
        }
    }

    private static string BuildItemList(List<Equipment> items)
    {
        List<string> names = new List<string>();

        foreach (Equipment item in items)
        {
            names.Add($"{item.Name} ({item.EquipmentCode})");
        }

        return string.Join(", ", names);
    }
}
