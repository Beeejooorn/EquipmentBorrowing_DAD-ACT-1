using EquipmentBorrowing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EquipmentBorrowing.Infrastructure.Persistence;

// Creates a new unit of work (with a brand new DbContext) every time it is asked.
public class EfUnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IDbContextFactory<EquipmentBorrowingDbContext> _contextFactory;

    public EfUnitOfWorkFactory(IDbContextFactory<EquipmentBorrowingDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IUnitOfWork Create()
    {
        EquipmentBorrowingDbContext context = _contextFactory.CreateDbContext();
        return new EfUnitOfWork(context);
    }
}
