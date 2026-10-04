using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfBorrowingRepository : IBorrowingRepository
{
    private readonly EquipmentBorrowingDbContext _context;

    public EfBorrowingRepository(EquipmentBorrowingDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(
        Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        await _context.Borrowings.AddAsync(borrowing, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        Borrowing borrowing, CancellationToken cancellationToken = default)
    {
        _context.Borrowings.Update(borrowing);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<Borrowing>> GetActiveBorrowingsByStudentAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Borrowings
            .AsNoTracking()
            .Where(b => b.StudentId == studentId
                     && b.Status == BorrowingStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Borrowing>> GetAllActiveBorrowingsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Borrowings
            .AsNoTracking()
            .Where(b => b.Status == BorrowingStatus.Active)
            .ToListAsync(cancellationToken);
    }
}