namespace EquipmentBorrowing.Application.Interfaces;

using EquipmentBorrowing.Domain;

public interface IEquipmentRepository
{
    // Tracked: the service may change the item and save it.
    Task<Equipment?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<List<Equipment>> GetAllAsync(
        CancellationToken cancellationToken = default);

    // Only items that are available AND have no active borrowing (BR-10).
    Task<List<Equipment>> GetAvailableAsync(
        CancellationToken cancellationToken = default);

    // Partial, case-insensitive match on equipment code or name. Empty text returns everything.
    Task<List<Equipment>> SearchAsync(
        string? searchText,
        CancellationToken cancellationToken = default);

    // Compares codes after trimming and ignoring upper/lower case (BR-05).
    Task<bool> EquipmentCodeExistsAsync(
        string equipmentCode,
        int? ignoreEquipmentId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Equipment equipment,
        CancellationToken cancellationToken = default);
}
