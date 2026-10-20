namespace MTU.TravellerMap.Data;

public class SectorEntity
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string Milieu { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Abbreviation { get; set; }
    public string? Tags { get; set; }
    public string? Credits { get; set; }
    public string? Source { get; set; }
    /// <summary>Set when systems were last imported; null if only the sector list entry has been stored.</summary>
    public DateTime? DataImportedUtc { get; set; }

    public List<SectorNameEntity> Names { get; set; } = [];
    public List<SubsectorEntity> Subsectors { get; set; } = [];
    public List<AllegianceEntity> Allegiances { get; set; } = [];
    public List<SystemEntity> Systems { get; set; } = [];
}

public class SectorNameEntity
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public SectorEntity Sector { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public string? Lang { get; set; }
    public string? Source { get; set; }
}

public class SubsectorEntity
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public SectorEntity Sector { get; set; } = null!;
    /// <summary>Letter A-P.</summary>
    public string Letter { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class AllegianceEntity
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public SectorEntity Sector { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class SystemEntity
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public SectorEntity Sector { get; set; } = null!;
    /// <summary>Subsector letter A-P derived from the hex location.</summary>
    public string SubsectorLetter { get; set; } = string.Empty;
    public string Hex { get; set; } = string.Empty;
    /// <summary>Parsecs trailing (+) / spinward (-) of Reference (Core 0140).</summary>
    public int RelX { get; set; }
    /// <summary>Parsecs rimward (+) / coreward (-) of Reference (Core 0140).</summary>
    public int RelY { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Uwp { get; set; } = string.Empty;
    public string Remarks { get; set; } = string.Empty;
    public string Ix { get; set; } = string.Empty;
    public string Ex { get; set; } = string.Empty;
    public string Cx { get; set; } = string.Empty;
    public string N { get; set; } = string.Empty;
    public string B { get; set; } = string.Empty;
    public string Z { get; set; } = string.Empty;
    public string Pbg { get; set; } = string.Empty;
    public string W { get; set; } = string.Empty;
    /// <summary>Allegiance code; matches <see cref="AllegianceEntity.Code"/> within the same sector.</summary>
    public string A { get; set; } = string.Empty;
    public string Stellar { get; set; } = string.Empty;
}
