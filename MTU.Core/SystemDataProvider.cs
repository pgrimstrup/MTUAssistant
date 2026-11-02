using System.Collections.Frozen;
using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

public sealed record CatalogueSector(int Id, string Name, int X, int Y, string Milieu);
public sealed record CatalogueBorder(int SectorId, string Path, string? Allegiance, string? Color);
public sealed record CatalogueRoute(int SectorId, string Start, string End, int StartOffsetX, int StartOffsetY, int EndOffsetX, int EndOffsetY, string? Allegiance, string? Color);
public sealed record CatalogueSubsector(int Id, int SectorId, string Letter, string Name);
public sealed record MainWorldData(string Name, string Uwp, string Remarks, string Ix, string Ex,
    string Cx, string Nobility, string Bases, string Zone, string Pbg, string WorldCount,
    string AllegianceCode, string? AllegianceName, string Stellar);

/// <summary>
/// Extension point for future stars, planets and moons. Concrete body records can add
/// physical and orbital data when available; no bodies or orbits are inferred today.
/// Body identifiers are local to the owning system; ParentId identifies an optional parent body.
/// </summary>
public abstract record SystemBodyData(string Id, string Name, string? ParentId);

public sealed record CatalogueSystem(int Id, int SectorId, string Hex, string SubsectorLetter,
    string SubsectorHex, int RelX, int RelY, MainWorldData MainWorld)
{
    public ImmutableArray<SystemBodyData> Bodies { get; init; } = [];
}

/// <summary>Immutable process-wide catalogue. IDs retain their database identity across consumers.</summary>
public sealed class SystemCatalogue
{
    public ImmutableArray<CatalogueSector> Sectors { get; }
    public ImmutableArray<CatalogueSubsector> Subsectors { get; }
    public ImmutableArray<CatalogueSystem> Systems { get; }
    public ImmutableArray<CatalogueBorder> Borders { get; }
    public ImmutableArray<CatalogueRoute> Routes { get; }
    public FrozenDictionary<int, CatalogueSector> SectorsById { get; }
    public FrozenDictionary<int, CatalogueSystem> SystemsById { get; }
    public FrozenDictionary<(string Milieu, int X, int Y, string Hex), CatalogueSystem> SystemsByHex { get; }
    public FrozenDictionary<(int SectorId, string Letter), CatalogueSubsector> SubsectorsByLetter { get; }

    internal SystemCatalogue(IEnumerable<CatalogueSector> sectors, IEnumerable<CatalogueSubsector> subsectors,
        IEnumerable<CatalogueSystem> systems, IEnumerable<CatalogueBorder> borders, IEnumerable<CatalogueRoute> routes)
    {
        Borders = borders.ToImmutableArray();
        Routes = routes.ToImmutableArray();
        Sectors = sectors.OrderBy(s => s.Name).ThenBy(s => s.Id).ToImmutableArray();
        Subsectors = subsectors.OrderBy(s => s.Name).ThenBy(s => s.Id).ToImmutableArray();
        Systems = systems.OrderBy(s => s.MainWorld.Name).ThenBy(s => s.Id).ToImmutableArray();
        SectorsById = Sectors.ToFrozenDictionary(s => s.Id);
        SystemsById = Systems.ToFrozenDictionary(s => s.Id);
        SubsectorsByLetter = Subsectors.ToFrozenDictionary(s => (s.SectorId, s.Letter));
        SystemsByHex = Systems.ToFrozenDictionary(s => {
            var sector = SectorsById[s.SectorId];
            return (sector.Milieu, sector.X, sector.Y, s.Hex);
        });
    }
}

/// <summary>
/// Loads once at startup, publishing only a complete immutable snapshot. Owns no DbContext
/// after initialization. Search, map and selection share this instance across requests/circuits.
/// Reloads publish a complete replacement snapshot.
/// </summary>
public sealed class SystemDataProvider(IServiceScopeFactory scopeFactory)
{
    private readonly object _initializationLock = new();
    private Task? _initialization;
    private SystemCatalogue? _catalogue;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    public event Action? Reloaded;

    public SystemCatalogue Catalogue => Volatile.Read(ref _catalogue)
        ?? throw new InvalidOperationException("Initialize the system data provider before serving requests.");

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Task initialization;
        lock (_initializationLock) initialization = _initialization ??= LoadAsync();
        // A caller can cancel its wait without cancelling the load shared by other callers.
        return initialization.WaitAsync(cancellationToken);
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await _reloadGate.WaitAsync(cancellationToken);
        try { await LoadAsync(); }
        finally { _reloadGate.Release(); }
        Reloaded?.Invoke();
    }

    private async Task LoadAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var sectors = await db.Sectors.AsNoTracking()
            .Select(s => new CatalogueSector(s.Id, s.Name, s.X, s.Y, s.Milieu)).ToListAsync();
        var subsectors = await db.Subsectors.AsNoTracking()
            .Select(s => new CatalogueSubsector(s.Id, s.SectorId, s.Letter, s.Name)).ToListAsync();
        var allegiances = (await db.Allegiances.AsNoTracking().ToListAsync())
            .ToDictionary(a => (a.SectorId, a.Code), a => a.Name);
        var borders = await db.Borders.AsNoTracking().Select(b => new CatalogueBorder(b.SectorId, b.Path, b.Allegiance, b.Color)).ToListAsync();
        var routes = await db.Routes.AsNoTracking().Select(r => new CatalogueRoute(r.SectorId, r.Start, r.End, r.StartOffsetX, r.StartOffsetY, r.EndOffsetX, r.EndOffsetY, r.Allegiance, r.Color)).ToListAsync();
        var rows = await db.Systems.AsNoTracking().ToListAsync();
        var systems = rows.Select(s => {
            var (letter, hex) = SubsectorPosition(s.Hex);
            return new CatalogueSystem(s.Id, s.SectorId, s.Hex, letter, hex, s.RelX, s.RelY,
                new MainWorldData(s.Name, s.Uwp, s.Remarks, s.Ix, s.Ex, s.Cx, s.N, s.B, s.Z, s.Pbg,
                    s.W, s.A, allegiances.GetValueOrDefault((s.SectorId, s.A)), s.Stellar));
        });
        Volatile.Write(ref _catalogue, new SystemCatalogue(sectors, subsectors, systems, borders, routes));
    }

    private static (string Letter, string Hex) SubsectorPosition(string hex)
    {
        if (hex.Length != 4 || !int.TryParse(hex[..2], out var x) || !int.TryParse(hex[2..], out var y)
            || x is < 1 or > 32 || y is < 1 or > 40) return ("", "");
        return (((char)('A' + (y - 1) / 10 * 4 + (x - 1) / 8)).ToString(),
            $"{(x - 1) % 8 + 1:00}{(y - 1) % 10 + 1:00}");
    }
}
