using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

public static class ServiceCollectionExtensions
{
    public static string ConnectionString(string path) => $"Data Source={path}";

    /// <summary>Registers the scoped <see cref="MtuDbContext"/> backed by a SQLite file.</summary>
    public static IServiceCollection AddMtuDatabase(this IServiceCollection services, string? databasePath = null)
    {
        databasePath ??= MtuDbContext.DefaultDatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        services.AddDbContext<MtuDbContext>(o => o.UseSqlite(ConnectionString(databasePath)));
        services.AddScoped<SectorImporter>();
        return services;
    }

    /// <summary>Creates the database if needed and applies pending migrations.</summary>
    public static void MigrateMtuDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<MtuDbContext>().Database.Migrate();
    }

}
