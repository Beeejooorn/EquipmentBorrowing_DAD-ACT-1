using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Infrastructure;

// All database registrations live here, so the Desktop project never has to
// configure EF Core or PostgreSQL itself.
public static class InfrastructureServices
{
    public static void AddInfrastructure(IServiceCollection services, string connectionString)
    {
        // A DbContext FACTORY (singleton) instead of one shared DbContext.
        // Every unit of work asks it for a new, short-lived DbContext.
        services.AddDbContextFactory<EquipmentBorrowingDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddSingleton<IUnitOfWorkFactory, EfUnitOfWorkFactory>();
        services.AddSingleton<IDatabaseErrorClassifier, DatabaseErrorClassifier>();
        services.AddSingleton<DatabaseInitializer>();
    }
}
