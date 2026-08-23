namespace EquipmentBorrowing.Infrastructure.Repositories;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class InMemoryEquipmentRepository : IEquipmentRepository
{
    private readonly List<Equipment> _equipment = new();

    public Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var equipment = _equipment.FirstOrDefault(e => e.Id == id);
        return Task.FromResult(equipment);
    }

    public Task UpdateAsync(Equipment equipment, CancellationToken cancellationToken = default)
    {
        var existing = _equipment.FirstOrDefault(e => e.Id == equipment.Id);

        if (existing is not null)
        {
            _equipment.Remove(existing);
            _equipment.Add(equipment);
        }

        return Task.CompletedTask;
    }
}