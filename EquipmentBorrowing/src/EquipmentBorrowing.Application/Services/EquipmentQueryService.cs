namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class EquipmentQueryService
{
    private readonly IEquipmentRepository _equipmentRepository;

    public EquipmentQueryService(IEquipmentRepository equipmentRepository)
    {
        _equipmentRepository = equipmentRepository;
    }

    public Task<IEnumerable<Equipment>> GetAllEquipmentAsync(CancellationToken cancellationToken = default)
    {
        return _equipmentRepository.GetAllAsync(cancellationToken);
    }
}