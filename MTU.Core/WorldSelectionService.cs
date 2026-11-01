using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

public sealed record SelectedWorld(string Name, string Sector, string Hex, string Uwp, string Pbg,
    string W, string Stellar, string Remarks, string AllegianceCode, string? AllegianceName);

public sealed class WorldSelectionService(IServiceScopeFactory scopeFactory)
{
    public async Task<SelectedWorld?> FindAsync(int sectorX, int sectorY, int column, int row)
    {
        if (column is < 1 or > 32 || row is < 1 or > 40) return null;
        var hex = $"{column:00}{row:00}";
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        return await db.Systems.AsNoTracking()
            .Where(w => w.Sector.Milieu == TileRefreshWorker.Milieu && w.Sector.X == sectorX && w.Sector.Y == sectorY && w.Hex == hex)
            .Select(w => new SelectedWorld(w.Name, w.Sector.Name, w.Hex, w.Uwp, w.Pbg,
                w.W, w.Stellar, w.Remarks, w.A,
                w.Sector.Allegiances.Where(a => a.Code == w.A).Select(a => a.Name).FirstOrDefault()))
            .SingleOrDefaultAsync();
    }
}
