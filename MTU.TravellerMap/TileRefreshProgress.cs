namespace MTU.TravellerMap;

/// <param name="CurrentScale">Null while system data is being imported.</param>
/// <param name="CurrentSubsector">Subsector letter when a subsector image is being downloaded.</param>
public record TileRefreshProgress(
    int Completed,
    int Total,
    int Failed,
    string? CurrentSector,
    SectorImageScale? CurrentScale,
    char? CurrentSubsector = null);
