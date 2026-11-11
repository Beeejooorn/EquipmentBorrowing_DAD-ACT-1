using System.Data;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EquipmentBorrowing.Infrastructure.Persistence;

// EF Core implementation of IUnitOfWork.
// It owns ONE DbContext. The three repositories all use that same DbContext,
// so their changes can be saved in one PostgreSQL transaction.
public class EfUnitOfWork : IUnitOfWork
{
    private readonly EquipmentBorrowingDbContext _context;
    private IDbContextTransaction? _transaction;

    public IStudentRepository Students { get; }
    public IEquipmentRepository Equipment { get; }
    public IBorrowingRepository Borrowings { get; }

    public EfUnitOfWork(EquipmentBorrowingDbContext context)
    {
        _context = context;
        Students = new EfStudentRepository(context);
        Equipment = new EfEquipmentRepository(context);
        Borrowings = new EfBorrowingRepository(context);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        // Serializable is PostgreSQL's strictest isolation level.
        // It makes concurrent transactions behave as if they ran one after another.
        // If two staff members try to issue the same item (or go over a student's limit)
        // at the same moment, PostgreSQL rejects one of them instead of saving both.
        _transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        // If CommitAsync was not called, disposing the transaction rolls it back.
        // Errors while closing (for example when the connection is already gone)
        // are ignored here, because the service has already decided what to report.
        try
        {
            if (_transaction != null)
            {
                await _transaction.DisposeAsync();
            }
        }
        catch (Exception)
        {
        }

        try
        {
            await _context.DisposeAsync();
        }
        catch (Exception)
        {
        }
    }
}
