using System.Collections.Frozen;
using System.Collections.Immutable;

namespace MTU.Core;

public sealed record MapSubsector(string Letter, string Name);
public sealed record MapOverviewWorld(string Hex, string Name, string Uwp, string Remarks);
public sealed record MapSector(int Id, string Name, int X, int Y, IReadOnlyList<string> Hexes,
    IReadOnlyList<MapSubsector> Subsectors, IReadOnlyList<MapOverviewWorld> Worlds);
public sealed record MapOverview(int MinSectorX, int MinSectorY, int Columns, int Rows,
    IReadOnlyList<MapSector> Sectors);
public sealed record MapWorld(string Hex, string Name, string Uwp, string Bases, string Zone,
    string Pbg, string Remarks, string Allegiance);

/// <summary>Cached map projections over the shared startup catalogue. No request-time database access.</summary>
public sealed class MapDataService
{
    private readonly SystemDataProvider _provider;
    private readonly object _gate = new();
    private SystemCatalogue? _source;
    private MapOverview _overview = null!;
    private FrozenDictionary<int, IReadOnlyList<MapWorld>> _worlds = null!;

    public MapDataService(SystemDataProvider provider) => _provider = provider;

    private void Update(SystemCatalogue data)
    {
        var sectors = data.Sectors.Where(s => s.Milieu == TileRefreshWorker.Milieu)
            .OrderBy(s => s.Y).ThenBy(s => s.X).ToArray();
        var worlds = data.Systems.ToLookup(s => s.SectorId);
        var subsectors = data.Subsectors.ToLookup(s => s.SectorId);
        _worlds = sectors.ToFrozenDictionary(s => s.Id, s => (IReadOnlyList<MapWorld>)worlds[s.Id]
            .OrderBy(w => w.Hex).Select(w => new MapWorld(w.Hex, w.MainWorld.Name, w.MainWorld.Uwp,
                w.MainWorld.Bases, w.MainWorld.Zone, w.MainWorld.Pbg, w.MainWorld.Remarks,
                w.MainWorld.AllegianceCode)).ToImmutableArray());
        var minX = Math.Min(TileRefreshWorker.MinX, sectors.Select(s => s.X).DefaultIfEmpty(TileRefreshWorker.MinX).Min());
        var minY = Math.Min(TileRefreshWorker.MinY, sectors.Select(s => s.Y).DefaultIfEmpty(TileRefreshWorker.MinY).Min());
        var maxX = Math.Max(TileRefreshWorker.MaxX, sectors.Select(s => s.X).DefaultIfEmpty(TileRefreshWorker.MaxX).Max());
        var maxY = Math.Max(TileRefreshWorker.MaxY, sectors.Select(s => s.Y).DefaultIfEmpty(TileRefreshWorker.MaxY).Max());
        _overview = new MapOverview(minX, minY, maxX - minX + 1, maxY - minY + 1,
            sectors.Select(s => new MapSector(s.Id, s.Name, s.X, s.Y,
                _worlds[s.Id].Select(w => w.Hex).ToImmutableArray(),
                subsectors[s.Id].Select(ss => new MapSubsector(ss.Letter, ss.Name)).ToImmutableArray(),
                _worlds[s.Id].Select(w => new MapOverviewWorld(w.Hex, w.Name, w.Uwp, w.Remarks)).ToImmutableArray()))
                .ToImmutableArray());
    }

    public Task<MapOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) { var data = _provider.Catalogue; if (!ReferenceEquals(_source, data)) { Update(data); _source = data; } return Task.FromResult(_overview); }
    }

    public Task<IReadOnlyList<MapWorld>?> GetSectorAsync(int sectorId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate) { var data = _provider.Catalogue; if (!ReferenceEquals(_source, data)) { Update(data); _source = data; } return Task.FromResult(_worlds.GetValueOrDefault(sectorId)); }
    }
}
