namespace MTU.TravellerMap.Models;

/// <summary>Sector metadata plus its subsectors, allegiances and systems, parsed from /data/{sector}.</summary>
public class SectorData
{
    public string Name { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public string? Abbreviation { get; set; }
    public string? Milieu { get; set; }
    public string? Credits { get; set; }
    public string? Source { get; set; }
    public List<SectorName> Names { get; set; } = [];
    public List<Subsector> Subsectors { get; set; } = [];
    public List<Allegiance> Allegiances { get; set; } = [];
    public List<StarSystem> Systems { get; set; } = [];
}

public class Subsector
{
    /// <summary>Letter A-P.</summary>
    public string Letter { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class Allegiance
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class StarSystem
{
    /// <summary>Four digit hex location, e.g. 1910.</summary>
    public string Hex { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Uwp { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    /// <summary>Importance extension, e.g. { 4 }.</summary>
    public string Ix { get; set; } = string.Empty;
    /// <summary>Economic extension, e.g. (C8G+2).</summary>
    public string Ex { get; set; } = string.Empty;
    /// <summary>Cultural extension, e.g. [7D3B].</summary>
    public string Cx { get; set; } = string.Empty;
    /// <summary>Nobility.</summary>
    public string N { get; set; } = string.Empty;
    /// <summary>Bases.</summary>
    public string B { get; set; } = string.Empty;
    /// <summary>Travel zone.</summary>
    public string Z { get; set; } = string.Empty;
    /// <summary>Population multiplier, planetoid belts, gas giants.</summary>
    public string Pbg { get; set; } = string.Empty;
    /// <summary>Worlds in the system.</summary>
    public string W { get; set; } = string.Empty;
    /// <summary>Allegiance code.</summary>
    public string A { get; set; } = string.Empty;
    public string Stellar { get; set; } = string.Empty;
}
