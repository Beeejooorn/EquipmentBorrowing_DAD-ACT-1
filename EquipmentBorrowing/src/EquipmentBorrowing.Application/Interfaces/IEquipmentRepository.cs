namespace EquipmentBorrowing.Application.Interfaces;

using EquipmentBorrowing.Domain;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Equipment equipment,
        CancellationToken cancellationToken = default);
}