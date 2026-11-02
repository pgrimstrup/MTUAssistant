namespace MTU.Core;

/// <summary>Filters the shared startup catalogue; no database access or separate preload.</summary>
public sealed class SearchService(SystemDataProvider provider) : ISearchService
{
    private const int MaxSectors = 10,
        MaxSubsectors = 15,
        MaxSystems = 25;

    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        string term,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        term = term?.Trim() ?? string.Empty;
        if (term.Length < ISearchService.MinimumTermLength)
            return Task.FromResult<IReadOnlyList<SearchResult>>([]);
        var data = provider.Catalogue;
        // Return fresh mutable UI DTOs; callers cannot mutate the shared catalogue.
        var sectors = data
            .Sectors.Where(s => s.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSectors)
            .Select(s => new SearchResult { SectorId = s.Id, SectorName = s.Name });
        var subsectors = data
            .Subsectors.Where(s => s.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSubsectors)
            .Select(s => new SearchResult
            {
                SectorId = s.SectorId,
                SectorName = data.SectorsById[s.SectorId].Name,
                SubsectorId = s.Id,
                SubsectorLetter = s.Letter,
                SubsectorName = s.Name,
            });
        var systems = data
            .Systems.Where(s => s.MainWorld.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(MaxSystems)
            .Select(s =>
            {
                data.SubsectorsByLetter.TryGetValue(
                    (s.SectorId, s.SubsectorLetter),
                    out var subsector
                );
                return new SearchResult
                {
                    SectorId = s.SectorId,
                    SectorName = data.SectorsById[s.SectorId].Name,
                    SystemId = s.Id,
                    SystemName = s.MainWorld.Name,
                    SectorHex = s.Hex,
                    SubsectorLetter = s.SubsectorLetter,
                    SubsectorHex = s.SubsectorHex,
                    SubsectorId = subsector?.Id ?? 0,
                    SubsectorName = subsector?.Name ?? "",
                };
            });
        return Task.FromResult<IReadOnlyList<SearchResult>>([
            .. sectors,
            .. subsectors,
            .. systems,
        ]);
    }
}
