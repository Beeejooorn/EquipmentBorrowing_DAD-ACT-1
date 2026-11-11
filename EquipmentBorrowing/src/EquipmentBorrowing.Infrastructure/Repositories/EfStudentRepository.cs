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

    public async Task<Student?> GetByIdAsync(
        int id, CancellationToken cancellationToken = default)
    {
        // Tracked: the service may modify it and save it afterwards.
        return await _context.Students
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<List<Student>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Students
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Student>> SearchAsync(
        string? searchText, CancellationToken cancellationToken = default)
    {
        string text = (searchText ?? "").Trim().ToLower();

        if (text == "")
        {
            return await GetAllAsync(cancellationToken);
        }

        return await _context.Students
            .AsNoTracking()
            .Where(s => s.Name.ToLower().Contains(text)
                     || s.StudentNumber.ToLower().Contains(text))
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> StudentNumberExistsAsync(
        string studentNumber, int? ignoreStudentId, CancellationToken cancellationToken = default)
    {
        // Same normalization as the database key column: trim + upper case.
        string key = studentNumber.Trim().ToUpperInvariant();

        if (ignoreStudentId == null)
        {
            return await _context.Students
                .AnyAsync(s => s.StudentNumber.Trim().ToUpper() == key, cancellationToken);
        }

        int idToIgnore = ignoreStudentId.Value;

        return await _context.Students
            .AnyAsync(s => s.Id != idToIgnore && s.StudentNumber.Trim().ToUpper() == key, cancellationToken);
    }

    public async Task AddAsync(
        Student student, CancellationToken cancellationToken = default)
    {
        await _context.Students.AddAsync(student, cancellationToken);
    }
}
