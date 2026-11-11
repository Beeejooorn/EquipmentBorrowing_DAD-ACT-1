using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EquipmentBorrowing.Infrastructure.Persistence;

// Used only by the "dotnet ef" command line tool (migrations add / database update).
// The running application does not use this class.
public class EquipmentBorrowingDbContextFactory
    : IDesignTimeDbContextFactory<EquipmentBorrowingDbContext>
{
    public EquipmentBorrowingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EquipmentBorrowingDbContext>()
            .UseNpgsql(DatabaseSettings.GetConnectionString())
            .Options;

        return new EquipmentBorrowingDbContext(options);
    }
}
