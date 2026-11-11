namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

// UC-15 Register Equipment, UC-16 Update Equipment Details.
public class EquipmentManagementService
{
    private const int CodeMaxLength = 30;
    private const int NameMaxLength = 100;
    private const int DescriptionMaxLength = 200;

    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IDatabaseErrorClassifier _errorClassifier;

    public EquipmentManagementService(IUnitOfWorkFactory unitOfWorkFactory, IDatabaseErrorClassifier errorClassifier)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _errorClassifier = errorClassifier;
    }

    // UC-15
    public async Task<OperationResult> RegisterEquipmentAsync(
        string? equipmentCode,
        string? name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        // Clean the input: remove spaces at the start/end,
        // and store a blank description as "no description" (null), see BR-06.
        string code = (equipmentCode ?? "").Trim();
        string itemName = (name ?? "").Trim();
        string? itemDescription = CleanOptionalText(description);

        string? validationError = ValidateEquipment(code, itemName, itemDescription);

        if (validationError != null)
        {
            return OperationResult.Rejected(validationError);
        }

        bool commitStarted = false;

        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            await unitOfWork.BeginTransactionAsync(cancellationToken);

            // BR-05: friendly check first. The unique index in PostgreSQL is the final guard.
            bool codeTaken = await unitOfWork.Equipment.EquipmentCodeExistsAsync(code, null, cancellationToken);

            if (codeTaken)
            {
                return OperationResult.Rejected(
                    $"Equipment code \"{code}\" is already registered " +
                    "(codes are compared ignoring upper/lower case and spaces). Nothing was saved.");
            }

            // BR-07: new equipment is available by default.
            Equipment equipment = new Equipment(0, code, itemName, itemDescription, IsAvailable: true);

            await unitOfWork.Equipment.AddAsync(equipment, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            commitStarted = true;
            await unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success(
                $"{equipment.Name} ({equipment.EquipmentCode}) was registered and is available for borrowing.",
                equipment.Id);
        }
        catch (Exception ex)
        {
            DatabaseErrorKind kind = _errorClassifier.Classify(ex);
            return DatabaseErrors.ToResult(kind, commitStarted);
        }
    }

    // UC-16: only code, name and description can change.
    // Availability is NOT a parameter here: it is changed only by borrowing and returning (BR-07).
    // The item keeps the same Id, so its borrowing history stays linked.
    public async Task<OperationResult> UpdateEquipmentAsync(
        int equipmentId,
        string? equipmentCode,
        string? name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        string code = (equipmentCode ?? "").Trim();
        string itemName = (name ?? "").Trim();
        string? itemDescription = CleanOptionalText(description);

        string? validationError = ValidateEquipment(code, itemName, itemDescription);

        if (validationError != null)
        {
            return OperationResult.Rejected(validationError);
        }

        bool commitStarted = false;

        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            await unitOfWork.BeginTransactionAsync(cancellationToken);

            Equipment? equipment = await unitOfWork.Equipment.GetByIdAsync(equipmentId, cancellationToken);

            if (equipment is null)
            {
                return OperationResult.NotFound("This equipment no longer exists. The list has been refreshed.");
            }

            // Another item may not already use this code (this item itself is ignored).
            bool codeTaken = await unitOfWork.Equipment.EquipmentCodeExistsAsync(code, equipmentId, cancellationToken);

            if (codeTaken)
            {
                return OperationResult.Rejected(
                    $"Equipment code \"{code}\" already belongs to another item. Nothing was saved.");
            }

            equipment.UpdateDetails(code, itemName, itemDescription);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            commitStarted = true;
            await unitOfWork.CommitAsync(cancellationToken);

            return OperationResult.Success(
                $"Details of {equipment.Name} ({equipment.EquipmentCode}) were updated. Availability was not changed.",
                equipment.Id);
        }
        catch (Exception ex)
        {
            DatabaseErrorKind kind = _errorClassifier.Classify(ex);
            return DatabaseErrors.ToResult(kind, commitStarted);
        }
    }

    // Returns an error message, or null when the input is valid.
    private static string? ValidateEquipment(string code, string name, string? description)
    {
        if (code == "")
        {
            return "Equipment code is required.";
        }

        if (code.Length > CodeMaxLength)
        {
            return $"Equipment code can have at most {CodeMaxLength} characters.";
        }

        if (name == "")
        {
            return "Equipment name is required.";
        }

        if (name.Length > NameMaxLength)
        {
            return $"Equipment name can have at most {NameMaxLength} characters.";
        }

        if (description != null && description.Length > DescriptionMaxLength)
        {
            return $"Description can have at most {DescriptionMaxLength} characters.";
        }

        return null;
    }

    // "   " or "" becomes null (no value). Other text is trimmed.
    private static string? CleanOptionalText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text.Trim();
    }
}
