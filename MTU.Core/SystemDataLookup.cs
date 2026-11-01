using System.Globalization;
using System.Text.RegularExpressions;

namespace MTU.Core;

public sealed record SystemStar(string Designation, string Code, string Description);

public sealed record SystemData(IReadOnlyList<SystemStar> Stars, bool StellarDataKnown,
    int? Worlds, int? GasGiants, int? AsteroidBelts, string WorldDescription);

/// <summary>System data and terminology displayed alongside the UWP.</summary>
/// <remarks>
/// Stellar and PBG codes: https://travellermap.com/doc/secondsurvey.
/// World counts follow the MTU convention: W plus the main world unless it is a belt or moon.
/// </remarks>
public static partial class SystemDataLookup
{
    private const string Digits = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private static readonly IReadOnlyDictionary<char, string> SpectralTypes = new Dictionary<char, string>
    {
        ['O'] = "Blue", ['B'] = "Blue-white", ['A'] = "White", ['F'] = "Yellow-white",
        ['G'] = "Yellow", ['K'] = "Orange", ['M'] = "Red",
        ['L'] = "L-type", ['T'] = "T-type", ['Y'] = "Y-type"
    };
    private static readonly IReadOnlyDictionary<string, string> LuminosityTypes = new Dictionary<string, string>
    {
        ["IA"] = "bright supergiant", ["IAB"] = "intermediate supergiant", ["IB"] = "weak supergiant",
        ["II"] = "bright giant", ["III"] = "giant", ["IV"] = "subgiant",
        ["V"] = "main-sequence star", ["VI"] = "subdwarf", ["VII"] = "white dwarf", ["D"] = "white dwarf"
    };
    private static readonly IReadOnlyDictionary<string, string> SpecialTypes = new Dictionary<string, string>
    {
        ["BD"] = "Brown dwarf", ["BH"] = "Black hole", ["NS"] = "Neutron star", ["PSR"] = "Pulsar"
    };

    // Consume complete classifications, not individual whitespace-separated words:
    // e.g. "F7 V BD M3 V" contains three stellar objects, not five.
    [GeneratedRegex(@"\G\s*(?:(?<special>PSR|BD|BH|NS|D[ABFOQZCGKMX]?)|(?<spectral>[OBAFGKMLTY])(?<subtype>[0-9]?)\s*(?<luminosity>IAB|IA|IB|III|II|IV|VII|VI|V|D))(?=\s|$)", RegexOptions.CultureInvariant)]
    private static partial Regex StarPattern();

    public static SystemData Describe(SelectedWorld world)
    {
        var uwp = world.Uwp.Trim().ToUpperInvariant();
        var size = uwp.Length == 9 && uwp[7] == '-' ? uwp[1] : '?';
        var knownSize = Digits.IndexOf(size) is >= 0 and <= 15 || size == 'S';
        var belt = size == '0';
        var moon = world.Remarks.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Contains("Sa", StringComparer.OrdinalIgnoreCase);

        int? worlds = null;
        if ((knownSize || moon) && int.TryParse(world.W.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            && count < int.MaxValue)
            worlds = count + (belt || moon ? 0 : 1);

        var belts = PbgDigit(world.Pbg, 1);
        // An unknown main-world size also leaves the belt adjustment unknown.
        int? asteroidBelts = knownSize ? belts + (belt ? 1 : 0) : null;
        var (stars, stellarDataKnown) = ParseStars(world.Stellar);
        var gasGiants = PbgDigit(world.Pbg, 2);
        return new SystemData(stars, stellarDataKnown, worlds, gasGiants, asteroidBelts,
            DescribeWorlds(worlds, gasGiants, asteroidBelts, belt, moon));
    }

    private static string DescribeWorlds(int? worlds, int? gasGiants, int? belts, bool mainWorldIsBelt, bool mainWorldIsMoon)
    {
        var description = worlds is { } count
            ? $"{count} {(count == 1 ? "world" : "worlds")}" : "Unknown number of worlds";
        var included = new List<string>();
        if (gasGiants is > 0)
            included.Add($"{gasGiants} {(gasGiants == 1 ? "gas giant" : "gas giants")}");
        if (belts is > 0)
            included.Add($"{belts} {(belts == 1 ? "asteroid belt" : "asteroid belts")}");
        if (included.Count > 0) description += ", including " + string.Join(" and ", included);
        description += ".";
        if (mainWorldIsBelt) description += " The main world is an asteroid belt.";
        else if (mainWorldIsMoon) description += " The main world is a satellite.";
        return description;
    }

    private static string StarDesignation(int index)
    {
        var designation = "";
        do
        {
            designation = (char)('A' + index % 26) + designation;
            index = index / 26 - 1;
        } while (index >= 0);
        return designation;
    }

    private static int? PbgDigit(string pbg, int position)
    {
        var text = pbg.Trim().ToUpperInvariant();
        if (text.Length != 3) return null;
        var value = Digits.IndexOf(text[position]);
        return value >= 0 ? value : null;
    }

    private static (IReadOnlyList<SystemStar> Stars, bool Known) ParseStars(string stellar)
    {
        var text = stellar.Trim().ToUpperInvariant();
        if (text.Length == 0) return ([], false);
        var stars = new List<SystemStar>();
        var position = 0;
        while (position < text.Length)
        {
            var match = StarPattern().Match(text, position);
            // Do not report a misleading count if any part of the stored data is unrecognised.
            if (!match.Success) return ([], false);
            if (match.Groups["spectral"].Success)
            {
                var spectral = match.Groups["spectral"].Value[0];
                var subtype = match.Groups["subtype"].Value;
                var luminosity = match.Groups["luminosity"].Value;
                stars.Add(new SystemStar(StarDesignation(stars.Count), $"{spectral}{subtype} {luminosity}",
                    $"{SpectralTypes[spectral]} {LuminosityTypes[luminosity]}"));
            }
            else
            {
                var code = match.Groups["special"].Value;
                stars.Add(new SystemStar(StarDesignation(stars.Count), code, code.StartsWith('D') ? "White dwarf" : SpecialTypes[code]));
            }
            position = match.Index + match.Length;
        }
        return (stars, true);
    }
}
