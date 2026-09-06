namespace EquipmentBorrowing.Application.Services;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class BorrowingQueryService
{
    private readonly IBorrowingRepository _borrowingRepository;

    public BorrowingQueryService(IBorrowingRepository borrowingRepository)
    {
        _borrowingRepository = borrowingRepository;
    }

    public Task<List<Borrowing>> GetAllActiveBorrowingsAsync(CancellationToken cancellationToken = default)
    {
        return _borrowingRepository.GetAllActiveBorrowingsAsync(cancellationToken);
    }
}