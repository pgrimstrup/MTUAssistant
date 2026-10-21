using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

/// <summary>
/// Search preloads all names to ensure searching is fast and responsive. Around 50,000 records are loaded.
/// </summary>
public class SearchService(IServiceScopeFactory scopeFactory) : ISearchService
{
    private const int MaxSectors = 10, MaxSubsectors = 15, MaxSystems = 25;

    readonly Dictionary<int, SearchResult> _sectors = new();
    readonly Dictionary<int, SearchResult> _subsectors = new();
    readonly Dictionary<int, SearchResult> _systems = new();

    bool _preloading = false;
    bool _preloadComplete = false;

    private async Task CheckPreload()
    {
        if (_preloading || _preloadComplete)
            return;

        _preloading = true;
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();

            var sectors = await db.Sectors.AsNoTracking()
                .Select(s => new SearchResult {
                    SectorId = s.Id,
                    SectorName = s.Name
                })
                .ToListAsync();
            foreach (var sector in sectors)
                _sectors[sector.SectorId] = sector;

            var subsectors = await db.Subsectors.AsNoTracking()
                .Select(s => new SearchResult {
                    SectorId = s.SectorId,
                    SectorName = s.Sector.Name,
                    SubsectorId = s.Id,
                    SubsectorLetter = s.Letter,
                    SubsectorName = s.Name
                })
                .ToListAsync();
            foreach (var subsector in subsectors)
                _subsectors[subsector.SubsectorId] = subsector;

            var systems = await db.Systems.AsNoTracking()
                .Select(s => new SearchResult {
                    SectorId = s.SectorId,
                    SectorName = s.Sector.Name,
                    SystemId = s.Id,
                    SystemName = s.Name,
                    SectorHex = s.Hex
                })
                .ToListAsync();
            foreach (var system in systems)
            {
                system.SubsectorLetter = CalculateSubsectorLetter(system.SectorHex);
                system.SubsectorHex = CalculateSubsectorHex(system.SectorHex);
                var ss = subsectors.FirstOrDefault(s => s.SectorId == system.SectorId && s.SubsectorLetter == system.SubsectorLetter);
                if (ss != null)
                {
                    system.SubsectorId = ss.SubsectorId;
                    system.SubsectorName = ss.SubsectorName;
                }
                _systems[system.SystemId] = system;
            }

        }
        finally
        {
            _preloadComplete = true;
            _preloading = false;
        }
    }

    private string CalculateSubsectorHex(string sectorHex)
    {
        if (sectorHex.Length < 4)
            return "";

        int.TryParse(sectorHex.Substring(0, 2), out int x);
        int.TryParse(sectorHex.Substring(2, 2), out int y);
        if (x > 0 && y > 0)
        {
            x = (x - 1) % 8 + 1;
            y = (y - 1) % 10 + 1;
            return $"{x:00}{y:00}";
        }
        return "";
    }

    private string CalculateSubsectorLetter(string sectorHex)
    {
        if (sectorHex.Length < 4)
            return "";

        int.TryParse(sectorHex.Substring(0, 2), out int x);
        int.TryParse(sectorHex.Substring(2, 2), out int y);
        if (x > 0 && y > 0)
        {
            x = (x - 1) / 8;
            y = (y - 1) / 10;
            return ((Char)('A' + (char)(y * 4 + x))).ToString();
        }
        return "";
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        await CheckPreload();

        term = term?.Trim() ?? string.Empty;
        if (term.Length < ISearchService.MinimumTermLength)
            return [];

        var sectors = _sectors.Values
            .Where(s => s.SectorName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.SectorName)
            .Take(MaxSectors)
            .ToList();
        var subsectors = _subsectors.Values
            .Where(s => s.SubsectorName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.SubsectorName)
            .Take(MaxSubsectors)
            .ToList();
        var systems = _systems.Values
            .Where(s => s.SystemName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.SystemName)
            .Take(MaxSystems)
            .ToList();

        return [.. sectors, .. subsectors, .. systems];
    }
}
