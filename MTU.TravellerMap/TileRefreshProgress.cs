namespace MTU.TravellerMap;

public record TileRefreshProgress(
    int Completed,
    int Total,
    int Failed,
    string? CurrentSector,
    SectorImageScale? CurrentScale);
