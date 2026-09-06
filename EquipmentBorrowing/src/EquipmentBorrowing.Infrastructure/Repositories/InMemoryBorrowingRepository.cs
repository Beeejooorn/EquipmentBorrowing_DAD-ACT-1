namespace EquipmentBorrowing.Infrastructure.Repositories;

using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;

public class InMemoryBorrowingRepository : IBorrowingRepository
{
    private readonly List<Borrowing> _borrowings = new();

    public Task AddAsync(Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        _borrowings.Add(borrowing);
        return Task.CompletedTask;
    }

    public Task<List<Borrowing>> GetActiveBorrowingsByStudentAsync(int studentId, CancellationToken cancellationToken = default)
    {
        var result = _borrowings.Where(b => b.StudentId == studentId && b.Status == BorrowingStatus.Active).ToList();
        return Task.FromResult(result);
    }

    public Task<List<Borrowing>> GetAllActiveBorrowingsAsync(CancellationToken cancellationToken = default)
    {
        var result = _borrowings.Where(b => b.Status == BorrowingStatus.Active).ToList();
        return Task.FromResult(result);
    }
}