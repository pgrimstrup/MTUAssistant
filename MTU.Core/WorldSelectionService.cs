namespace MTU.Core;

public sealed record SelectedWorld(string Name, string Sector, string Hex, string Uwp, string Pbg,
    string W, string Stellar, string Remarks, string AllegianceCode, string? AllegianceName, int SectorId = 0);

public sealed class WorldSelectionService(SystemDataProvider provider)
{
    public Task<SelectedWorld?> FindAsync(int sectorX, int sectorY, int column, int row)
    {
        if (column is < 1 or > 32 || row is < 1 or > 40) return Task.FromResult<SelectedWorld?>(null);
        var data = provider.Catalogue;
        if (!data.SystemsByHex.TryGetValue((TileRefreshWorker.Milieu, sectorX, sectorY, $"{column:00}{row:00}"), out var system))
            return Task.FromResult<SelectedWorld?>(null);
        var world = system.MainWorld;
        return Task.FromResult<SelectedWorld?>(new SelectedWorld(world.Name, data.SectorsById[system.SectorId].Name,
            system.Hex, world.Uwp, world.Pbg, world.WorldCount, world.Stellar, world.Remarks,
            world.AllegianceCode, world.AllegianceName, system.SectorId));
    }
}
