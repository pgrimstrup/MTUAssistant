namespace MTU.Core;

/// <summary>Progress measured in sectors, including failures.</summary>
public record TileRefreshProgress(int Completed, int Total, int Failed, string? CurrentSector);
