namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

// UC-03 Borrow Equipment.
public class BorrowEquipmentService
{
    private const int MaxActiveBorrowings = 3;

    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IDatabaseErrorClassifier _errorClassifier;

    public BorrowEquipmentService(
        IUnitOfWorkFactory unitOfWorkFactory,
        IDatabaseErrorClassifier errorClassifier)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _errorClassifier = errorClassifier;
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

        return IssueAsync(studentId, equipmentIds, expectedReturnDate, cancellationToken);
    }

    private async Task<OperationResult> IssueAsync(
        int studentId,
        List<int> equipmentIds,
        DateOnly? expectedReturnDate,
        CancellationToken cancellationToken)
    {
        // ---- Step 1: checks that do not need the database ----

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
            }

            // ---- Step 4: commit, everything becomes permanent together ----

            commitStarted = true;
            await unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success(
                $"{BuildItemList(itemsToIssue)} issued to {student.Name}. " +
                $"Expected return: {expectedReturnDate.Value:yyyy-MM-dd}.");
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
