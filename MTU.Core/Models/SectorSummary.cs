using System.Text.Json.Serialization;

namespace MTU.Core.Models;

/// <summary>An entry in the /data sector list.</summary>
public class SectorSummary
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Milieu { get; set; } = string.Empty;
    public string? Abbreviation { get; set; }
    public string? Tags { get; set; }
    public List<SectorName> Names { get; set; } = [];

    [JsonIgnore]
    public string Name => Names.FirstOrDefault()?.Text ?? Abbreviation ?? string.Empty;
}

public class SectorName
{
    public string Text { get; set; } = string.Empty;
    public string? Lang { get; set; }
    public string? Source { get; set; }
}


