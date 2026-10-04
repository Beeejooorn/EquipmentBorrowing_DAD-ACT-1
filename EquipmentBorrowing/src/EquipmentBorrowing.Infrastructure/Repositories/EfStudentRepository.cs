using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfStudentRepository : IStudentRepository
{
    private readonly EquipmentBorrowingDbContext _context;

    public EfStudentRepository(EquipmentBorrowingDbContext context)
    {
        _context = context;
    }

    public async Task<Student?> GetStudentAsync(
        int id, CancellationToken cancellationToken = default)
    {
        return await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Student>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Students
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}