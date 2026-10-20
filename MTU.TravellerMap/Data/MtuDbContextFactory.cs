using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MTU.TravellerMap.Data;

/// <summary>Used by the EF tooling (dotnet ef migrations ...) only.</summary>
public class MtuDbContextFactory : IDesignTimeDbContextFactory<MtuDbContext>
{
    public MtuDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(ServiceCollectionExtensions.ConnectionString(MtuDbContext.DefaultDatabasePath)).Options);
}
