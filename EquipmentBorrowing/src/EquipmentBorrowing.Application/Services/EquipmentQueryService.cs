namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
using EquipmentBorrowing.Domain;

// Read-only equipment queries: UC-01 View Equipment Catalog, UC-02 Find Available Equipment.
public class EquipmentQueryService
{
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly IDatabaseErrorClassifier _errorClassifier;

    public EquipmentQueryService(IUnitOfWorkFactory unitOfWorkFactory, IDatabaseErrorClassifier errorClassifier)
    {
        _unitOfWorkFactory = unitOfWorkFactory;
        _errorClassifier = errorClassifier;
    }

    // UC-01
    public async Task<QueryResult<Equipment>> GetAllEquipmentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            List<Equipment> equipment = await unitOfWork.Equipment.GetAllAsync(cancellationToken);
            return QueryResult<Equipment>.Ok(equipment);
        }
        catch (Exception ex)
        {
            return QueryResult<Equipment>.Error(DatabaseErrors.ToLoadMessage(_errorClassifier.Classify(ex)));
        }
    }

    // UC-02
    public async Task<QueryResult<Equipment>> GetAvailableEquipmentAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            List<Equipment> equipment = await unitOfWork.Equipment.GetAvailableAsync(cancellationToken);
            return QueryResult<Equipment>.Ok(equipment);
        }
        catch (Exception ex)
        {
            return QueryResult<Equipment>.Error(DatabaseErrors.ToLoadMessage(_errorClassifier.Classify(ex)));
        }
    }

    // Used by the Manage Equipment screen to locate an item (UC-16).
    public async Task<QueryResult<Equipment>> SearchEquipmentAsync(string? searchText, CancellationToken cancellationToken = default)
    {
        try
        {
            await using IUnitOfWork unitOfWork = _unitOfWorkFactory.Create();
            List<Equipment> equipment = await unitOfWork.Equipment.SearchAsync(searchText, cancellationToken);
            return QueryResult<Equipment>.Ok(equipment);
        }
        catch (Exception ex)
        {
            return QueryResult<Equipment>.Error(DatabaseErrors.ToLoadMessage(_errorClassifier.Classify(ex)));
        }
    }
}
