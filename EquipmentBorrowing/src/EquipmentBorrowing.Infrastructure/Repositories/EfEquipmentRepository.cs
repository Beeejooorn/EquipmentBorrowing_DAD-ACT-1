using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Domain;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Repositories;

public class EfEquipmentRepository : IEquipmentRepository
{
    private readonly EquipmentBorrowingDbContext _context;

    public EfEquipmentRepository(EquipmentBorrowingDbContext context)
    {
        _context = context;
    }

    public async Task<Equipment?> GetByIdAsync(
        int id, CancellationToken cancellationToken = default)
    {
        // Tracked: the service may modify it and save it afterwards.
        return await _context.Equipment
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<List<Equipment>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        // Display only: no tracking.
        return await _context.Equipment
            .AsNoTracking()
            .OrderBy(e => e.EquipmentCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Equipment>> GetAvailableAsync(
        CancellationToken cancellationToken = default)
    {
        // Available means: the flag is true AND there is no active borrowing (BR-10).
        return await _context.Equipment
            .AsNoTracking()
            .Where(e => e.IsAvailable
                     && !_context.Borrowings.Any(b => b.EquipmentId == e.Id && b.Status == BorrowingStatus.Active))
            .OrderBy(e => e.EquipmentCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Equipment>> SearchAsync(
        string? searchText, CancellationToken cancellationToken = default)
    {
        string text = (searchText ?? "").Trim().ToLower();

        if (text == "")
        {
            return await GetAllAsync(cancellationToken);
        }

        return await _context.Equipment
            .AsNoTracking()
            .Where(e => e.Name.ToLower().Contains(text)
                     || e.EquipmentCode.ToLower().Contains(text))
            .OrderBy(e => e.EquipmentCode)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> EquipmentCodeExistsAsync(
        string equipmentCode, int? ignoreEquipmentId, CancellationToken cancellationToken = default)
    {
        // Same normalization as the database key column: trim + upper case.
        string key = equipmentCode.Trim().ToUpperInvariant();

        if (ignoreEquipmentId == null)
        {
            return await _context.Equipment
                .AnyAsync(e => e.EquipmentCode.Trim().ToUpper() == key, cancellationToken);
        }

        int idToIgnore = ignoreEquipmentId.Value;

        return await _context.Equipment
            .AnyAsync(e => e.Id != idToIgnore && e.EquipmentCode.Trim().ToUpper() == key, cancellationToken);
    }

    public async Task AddAsync(
        Equipment equipment, CancellationToken cancellationToken = default)
    {
        await _context.Equipment.AddAsync(equipment, cancellationToken);
    }
}
