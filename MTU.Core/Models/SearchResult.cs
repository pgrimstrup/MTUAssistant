namespace MTU.Core.Models;

public enum SearchResultKind { Sector, Subsector, System }

/// <param name="Kind">What the result refers to.</param>
/// <param name="DisplayText">Text shown in the search list, e.g. "Regina (Regina 1910)".</param>
/// <param name="SectorId">Id of the owning sector (the sector itself for sector results).</param>
/// <param name="Id">Id of the sector, subsector or system row, depending on the kind.</param>
/// <param name="SubsectorLetter">Subsector letter A-P for subsector and system results.</param>
/// <param name="Hex">Four digit hex for system results.</param>
public record SearchResult(
    SearchResultKind Kind,
    string DisplayText,
    int SectorId,
    int Id,
    string? SubsectorLetter = null,
    string? Hex = null);
