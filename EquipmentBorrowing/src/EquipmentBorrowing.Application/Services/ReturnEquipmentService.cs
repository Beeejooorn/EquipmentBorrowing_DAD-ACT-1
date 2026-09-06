namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class ReturnEquipmentService
{
    private readonly IEquipmentRepository _equipmentRepository;

    public ReturnEquipmentService(IEquipmentRepository equipmentRepository)
    {
        _equipmentRepository = equipmentRepository;
    }

    public async Task<ReturnResult> ReturnAsync(
        Borrowing borrowing,
        CancellationToken cancellationToken = default)
    {
        if (borrowing.Status == BorrowingStatus.Returned)
        {
            return new ReturnResult(false, "This borrowing has already been returned.");
        }

        var equipment = await _equipmentRepository.GetByIdAsync(borrowing.EquipmentId, cancellationToken);

        if (equipment is null)
        {
            return new ReturnResult(false, "Associated equipment record not found.");
        }

        borrowing.MarkAsReturned();
        equipment.MarkAsAvailable();

        await _equipmentRepository.UpdateAsync(equipment, cancellationToken);

        return new ReturnResult(true, null);
    }
}