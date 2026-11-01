using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MTU.Core.Models;

public sealed class SectorMetadata
{
    public string? Stylesheet { get; set; }
    public List<MetadataAllegiance> Allegiances { get; set; } = [];
    public List<MetadataSubsector> Subsectors { get; set; } = [];
    public List<MetadataBorder> Borders { get; set; } = [];
    public List<MetadataRoute> Routes { get; set; } = [];
}
public sealed class MetadataAllegiance { public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string? Base { get; set; } public string? ShortName { get; set; } }
public sealed class MetadataSubsector { public string Index { get; set; } = ""; public string Name { get; set; } = ""; }
public sealed class MetadataBorder
{
    public string Path { get; set; } = "";
    public string? Allegiance { get; set; }
    public string? Color { get; set; }
    public string? Style { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
public sealed class MetadataRoute
{
    public string Start { get; set; } = ""; public string End { get; set; } = "";
    public int StartOffsetX { get; set; } public int StartOffsetY { get; set; }
    public int EndOffsetX { get; set; } public int EndOffsetY { get; set; }
    public string? Allegiance { get; set; } public string? Type { get; set; }
    public string? Color { get; set; } public string? Style { get; set; } public double? Width { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>Resolves TravellerMap's CSS-like type/class color rules; later matching rules win.</summary>
public static class MetadataStyles
{
    public static string? Color(string? stylesheet, string kind, params string?[] classes)
    {
        if (string.IsNullOrWhiteSpace(stylesheet)) return null;
        var text = Regex.Replace(stylesheet, @"/\*.*?\*/", "", RegexOptions.Singleline);
        string? color = null;
        var specificity = -1;
        foreach (Match rule in Regex.Matches(text, @"([^{}]+)\{([^{}]*)\}"))
        {
            var selectors = rule.Groups[1].Value.Split(',');
            var matchingSpecificity = -1;
            foreach (var selector in selectors)
            {
                var value = Regex.Replace(selector.Trim(), @"\\(.)", "$1");
                if (value.Equals(kind, StringComparison.OrdinalIgnoreCase)) matchingSpecificity = Math.Max(matchingSpecificity, 0);
                if (classes.Any(c => !string.IsNullOrEmpty(c) && value.Equals(kind + "." + c, StringComparison.OrdinalIgnoreCase))) matchingSpecificity = 1;
            }
            if (matchingSpecificity < specificity || matchingSpecificity < 0) continue;
            foreach (var declaration in rule.Groups[2].Value.Split(';'))
            {
                var pair = declaration.Split(':', 2);
                if (pair.Length == 2 && pair[0].Trim().Equals("color", StringComparison.OrdinalIgnoreCase))
                {
                    color = pair[1].Trim();
                    specificity = matchingSpecificity;
                }
            }
        }
        return string.IsNullOrWhiteSpace(color) ? null : color;
    }
}
