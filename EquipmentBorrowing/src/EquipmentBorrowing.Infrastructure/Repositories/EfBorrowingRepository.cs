using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Models;
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
    }

    public async Task<Borrowing?> GetByIdAsync(
        int id, CancellationToken cancellationToken = default)
    {
        // Tracked: the return service changes it and saves it.
        return await _context.Borrowings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<int> CountActiveByStudentAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        return await _context.Borrowings
            .CountAsync(b => b.StudentId == studentId
                          && b.Status == BorrowingStatus.Active, cancellationToken);
    }

    public async Task<bool> HasActiveBorrowingAsync(
        int equipmentId, CancellationToken cancellationToken = default)
    {
        return await _context.Borrowings
            .AnyAsync(b => b.EquipmentId == equipmentId
                        && b.Status == BorrowingStatus.Active, cancellationToken);
    }

    // UC-04
    public async Task<List<BorrowingRecord>> GetActiveRecordsAsync(
        CancellationToken cancellationToken = default)
    {
        IQueryable<Borrowing> query = _context.Borrowings
            .AsNoTracking()
            .Where(b => b.Status == BorrowingStatus.Active)
            .OrderBy(b => b.ExpectedReturnDate)
            .ThenBy(b => b.Id);

        return await ToRecords(query).ToListAsync(cancellationToken);
    }

    // UC-07: only this student's borrowings, active and returned.
    public async Task<List<BorrowingRecord>> GetStudentHistoryAsync(
        int studentId, CancellationToken cancellationToken = default)
    {
        IQueryable<Borrowing> query = _context.Borrowings
            .AsNoTracking()
            .Where(b => b.StudentId == studentId)
            .OrderByDescending(b => b.DateBorrowed)
            .ThenByDescending(b => b.Id);

        return await ToRecords(query).ToListAsync(cancellationToken);
    }

    // UC-08: only this item's borrowings, active and returned.
    public async Task<List<BorrowingRecord>> GetEquipmentHistoryAsync(
        int equipmentId, CancellationToken cancellationToken = default)
    {
        IQueryable<Borrowing> query = _context.Borrowings
            .AsNoTracking()
            .Where(b => b.EquipmentId == equipmentId)
            .OrderByDescending(b => b.DateBorrowed)
            .ThenByDescending(b => b.Id);

        return await ToRecords(query).ToListAsync(cancellationToken);
    }

    // UC-09: search text + status filter + sort (BR-17).
    // Each part is added only if the user used it, so criteria can be combined freely.
    public async Task<List<BorrowingRecord>> SearchAsync(
        BorrowingSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        IQueryable<Borrowing> query = _context.Borrowings.AsNoTracking();

        // 1. Search text (trimmed, case-insensitive, partial match).
        string text = (criteria.SearchText ?? "").Trim().ToLower();

        if (text != "")
        {
            query = query.Where(b => b.Student!.Name.ToLower().Contains(text)
                                  || b.Student!.StudentNumber.ToLower().Contains(text)
                                  || b.Equipment!.Name.ToLower().Contains(text));
        }

        // 2. Status filter.
        if (criteria.Status == BorrowingStatusFilter.Active)
        {
            query = query.Where(b => b.Status == BorrowingStatus.Active);
        }
        else if (criteria.Status == BorrowingStatusFilter.Returned)
        {
            query = query.Where(b => b.Status == BorrowingStatus.Returned);
        }

        // 3. Sorting. The borrowing Id is always the tie-breaker,
        //    so rows with the same date always come out in the same order.
        if (criteria.SortBy == BorrowingSortField.CheckoutDate)
        {
            if (criteria.SortDescending)
            {
                query = query.OrderByDescending(b => b.DateBorrowed).ThenBy(b => b.Id);
            }
            else
            {
                query = query.OrderBy(b => b.DateBorrowed).ThenBy(b => b.Id);
            }
        }
        else
        {
            if (criteria.SortDescending)
            {
                query = query.OrderByDescending(b => b.ExpectedReturnDate).ThenBy(b => b.Id);
            }
            else
            {
                query = query.OrderBy(b => b.ExpectedReturnDate).ThenBy(b => b.Id);
            }
        }

        return await ToRecords(query).ToListAsync(cancellationToken);
    }

    // Turns borrowings into display records that include the student and equipment details.
    // EF Core translates this into one SQL query with JOINs, it does not load rows one by one.
    private static IQueryable<BorrowingRecord> ToRecords(IQueryable<Borrowing> query)
    {
        return query.Select(b => new BorrowingRecord
        {
            BorrowingId = b.Id,
            StudentId = b.StudentId,
            StudentNumber = b.Student!.StudentNumber,
            StudentName = b.Student!.Name,
            EquipmentId = b.EquipmentId,
            EquipmentCode = b.Equipment!.EquipmentCode,
            EquipmentName = b.Equipment!.Name,
            DateBorrowed = b.DateBorrowed,
            ExpectedReturnDate = b.ExpectedReturnDate,
            ReturnedAt = b.ReturnedAt,
            Status = b.Status
        });
    }
}
