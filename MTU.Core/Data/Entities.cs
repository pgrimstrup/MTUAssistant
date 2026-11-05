namespace MTU.Core.Data;

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
    public List<BorderEntity> Borders { get; set; } = [];
    public List<RouteEntity> Routes { get; set; } = [];
    public string? Stylesheet { get; set; }
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
    public string? Base { get; set; }
    public string? ShortName { get; set; }
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

public class BorderEntity
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public SectorEntity Sector { get; set; } = null!;
    public string Path { get; set; } = "";
    public string? Allegiance { get; set; }
    public string? Color { get; set; }
    public string? Style { get; set; }
    public string RawJson { get; set; } = "{}";
}

public class RouteEntity
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public SectorEntity Sector { get; set; } = null!;
    public string Start { get; set; } = "";
    public string End { get; set; } = "";
    public int StartOffsetX { get; set; }
    public int StartOffsetY { get; set; }
    public int EndOffsetX { get; set; }
    public int EndOffsetY { get; set; }
    public string? Allegiance { get; set; }
    public string? Type { get; set; }
    public string? Color { get; set; }
    public string? Style { get; set; }
    public double? Width { get; set; }
    public string RawJson { get; set; } = "{}";
}

/// <summary>Per-sector/hex market survives system-row replacement during sector imports.</summary>
public class TradeMarketEntity
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public SectorEntity Sector { get; set; } = null!;
    public string Hex { get; set; } = "";
    public string Mode { get; set; } = "";
    public string ResultJson { get; set; } = "";
}

/// <summary>Transport results remain associated with a sector and hex across data refreshes.</summary>
public class TransportMarketEntity
{
    public int Id { get; set; }
    public int SectorId { get; set; }
    public SectorEntity Sector { get; set; } = null!;
    public string Hex { get; set; } = "";
    public string Kind { get; set; } = "";
    public string ResultJson { get; set; } = "";
}

public class CampaignEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Day { get; set; } = 1;
    public int Year { get; set; } = 1105;
    public int? LocationSectorId { get; set; }
    public string? LocationHex { get; set; }
    public bool IsActive { get; set; }
}

public class CharacterEntity
{
    public bool IsNpc { get; set; }

    public int Id { get; set; }
    public int CampaignId { get; set; }
    public CampaignEntity Campaign { get; set; } = null!;
    public string PlayerName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Race { get; set; } = "";
    public string Upp { get; set; } = "";
    public string TermsServed { get; set; } = "0";
    public int HighestNavalRank { get; set; }
    public int HighestScoutRank { get; set; }
    public int NaturalDr { get; set; }
    public int Speed { get; set; } = 6;
    public InjuryTolerance InjuryTolerance { get; set; }
    public byte[]? TokenImage { get; set; }
    public bool Inactive { get; set; }
}

public class CharacterSkillEntity
{
    public int Id { get; set; }
    public int CharacterId { get; set; }
    public CharacterEntity Character { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Level { get; set; }
}

public class CharacterEquipmentEntity
{
    public bool XRayOutliningAid { get; set; }
    public bool Equipped { get; set; }
    public int Quantity { get; set; } = 1;
    public bool Scope { get; set; }
    public bool TargetingSystem { get; set; }
    public bool LaserSight { get; set; }
    public bool HolographicSight { get; set; }
    public bool Fof { get; set; }
    public bool Phud { get; set; }
    public bool SmartTracker { get; set; }
    public int CharacterId { get; set; }
    public CharacterEntity Character { get; set; } = null!;
    public int EquipmentId { get; set; }
    public EquipmentItem Equipment { get; set; } = null!;
}

public class NpcEntity
{
    public NpcType Type { get; set; }
    public int? Age { get; set; }
    public int CharacterId { get; set; }
    public CharacterEntity Character { get; set; } = null!;
    public string Occupation { get; set; } = "";
    public string Gender { get; set; } = "";
    public string Quirks1 { get; set; } = "";
    public string Quirks2 { get; set; } = "";
    public NpcSkillLevel SkillLevel { get; set; } = NpcSkillLevel.Novice;
}
