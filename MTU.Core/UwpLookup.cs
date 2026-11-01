namespace MTU.Core;

public sealed record UwpAttribute(string Name, char Code, string Description);

/// <summary>UWP descriptions for the M1105 map. Edit terminology here for your universe.</summary>
/// <remarks>Reference: https://travellermap.com/doc/secondsurvey (Second Survey UWP).</remarks>
public static class UwpLookup
{
    private const string Digits = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    // Second Survey size table, in Earth gravities; assumes Earth-like density.
    // Index zero is unused because an asteroid belt has no single surface gravity.
    private static readonly double[] SizeGravity = [0, 0.122, 0.240, 0.377, 0.500, 0.625, 0.840, 0.875, 1.000, 1.120, 1.250, 1.375, 1.500, 1.625, 1.750, 1.875];
    private static readonly string[] Names = ["Starport", "Size", "Atmosphere", "Hydrographics", "Population", "Government", "Law level", "Tech level"];
    private static readonly string[] Ports = ["Excellent; refined fuel, starship construction and overhaul", "Good; refined fuel, spacecraft construction and overhaul", "Routine; unrefined fuel and repairs", "Poor; unrefined fuel, no repairs", "Frontier landing site; no fuel or facilities"];
    private static readonly string[] Atmospheres = ["Vacuum", "Trace atmosphere", "Very thin, tainted", "Very thin", "Thin, tainted", "Thin", "Standard", "Standard, tainted", "Dense", "Dense, tainted", "Exotic", "Corrosive", "Insidious", "Dense; breathable at altitude", "Thin; breathable in lowlands", "Unusual atmosphere"];
    private static readonly string[] Governments = ["No formal government", "Corporate rule", "Direct democracy", "Self-perpetuating oligarchy", "Representative democracy", "Feudal technocracy", "Colony or captive government", "Multiple independent governments", "Civil service bureaucracy", "Impersonal bureaucracy", "Charismatic dictatorship", "Non-charismatic dictatorship", "Charismatic oligarchy", "Religious dictatorship", "Religious autocracy", "Totalitarian oligarchy", "Aslan facility", "Divided Aslan clans", "Single local Aslan clan", "Single interstellar Aslan clan", "Major Aslan clan", "Vassal Aslan clan", "Major vassal Aslan clan", "K'kree facility", "Off-world steppelord's representative", "Resident steppelord", "Hiver sept", "Unsupervised anarchy", "Supervised anarchy"];
    private static readonly string[] Laws = ["No restrictions", "Explosives, poison gas and body pistols banned", "Portable energy weapons banned", "Automatic weapons banned", "Assault weapons banned", "Concealable weapons banned", "Firearms other than shotguns banned", "Shotguns banned", "Carrying long blades restricted", "Weapons restricted to the home", "All weapons banned", "Civilian travel tightly controlled", "Privacy unprotected", "Paramilitary policing", "Police state", "Daily life comprehensively regulated", "Harsh penalties for minor offences", "Oppression legally sanctioned", "Routine oppression", "Extreme oppression", "Total oppression"];
    private static readonly string[] Technologies = ["Stone-age tools", "Bronze and iron tools", "Printing", "Early science", "Steam power", "Industrial mass production", "Nuclear technology", "Miniaturised electronics", "Advanced computers", "Early gravitic technology", "Interstellar society", "Early Imperial technology", "Typical Imperial technology", "Advanced Imperial technology", "Advanced Imperial technology", "Imperial technological peak", "Advanced robotics", "Artificial intelligence", "Personal disintegration technology", "Plastic metals", "Technology beyond ordinary comprehension"];

    public static IReadOnlyList<UwpAttribute> Describe(string? uwp, string? pbg = null)
    {
        var text = uwp?.Trim().ToUpperInvariant() ?? "";
        // Do not shift fields when stored data is incomplete or malformed.
        var valid = text.Length == 9 && text[7] == '-';
        return Names.Select((name, i) => {
            var code = valid ? text[i == 7 ? 8 : i] : '?';
            var description = DescribeCode(i, code);
            if (i == 3 && valid) description += LandAreaDescription(text[1], code);
            if (i == 4) description += PopulationDescription(code, pbg);
            return new UwpAttribute(name, code, description);
        }).ToArray();
    }

    private static string LandAreaDescription(char sizeCode, char hydrographicsCode)
    {
        var size = Digits.IndexOf(sizeCode);
        var hydrographics = Digits.IndexOf(hydrographicsCode);
        // Asteroid belts and small/unknown worlds have no usable spherical diameter.
        if (size is < 1 or > 15 || hydrographics is < 0 or > 10) return "";

        var diameterKm = size * 1600d;
        var surfaceAreaKm2 = Math.PI * diameterKm * diameterKm; // 4πr² = πd²
        var minimumLiquidPercent = Math.Clamp(hydrographics * 10 - 5, 0, 100);
        var maximumLiquidPercent = Math.Clamp(hydrographics * 10 + 5, 0, 100);
        var minimumLandAreaKm2 = surfaceAreaKm2 * (100 - maximumLiquidPercent) / 100;
        var maximumLandAreaKm2 = surfaceAreaKm2 * (100 - minimumLiquidPercent) / 100;
        return $". Approximate land area: {minimumLandAreaKm2:N0}–{maximumLandAreaKm2:N0} km².";
    }

    private static string PopulationDescription(char code, string? pbg)
    {
        var exponent = Digits.IndexOf(code);
        var multiplierCode = pbg?.Trim().FirstOrDefault() ?? '\0';
        if (exponent is < 0 or > 15 || multiplierCode is < '0' or > '9') return "";

        var multiplier = multiplierCode - '0';
        long magnitude = 1;
        for (var i = 0; i < exponent; i++) magnitude *= 10;
        var population = magnitude * multiplier;
        return $". Population: {population:N0}.";
    }

    private static string DescribeCode(int field, char code)
    {
        if (code == '?') return "Unknown";
        int n = Digits.IndexOf(code);
        string[]? table = field switch { 2 => Atmospheres, 5 => Governments, 6 => Laws, 7 => Technologies, _ => null };
        if (table is not null && n >= 0 && n < table.Length) return table[n];
        return field switch {
            0 when code is >= 'A' and <= 'E' => Ports[code - 'A'],
            0 when code == 'X' => "No starport",
            1 when n == 0 => "Asteroid belt",
            1 when n is > 0 and <= 15 => $"Approximately {n * 1600:N0} km diameter. Surface gravity: {SizeGravity[n]:0.###} g",
            1 when code == 'S' => "Small world (under 1,600 km)",
            3 when n is >= 0 and <= 10 => $"Approximately {n * 10}% surface liquid",
            4 when n == 0 => "Uninhabited",
            4 when n is > 0 and <= 15 => $"Population order of magnitude: 10^{n} inhabitants",
            5 when code == 'W' => "Hiver committee",
            5 when code == 'X' => "Droyne hierarchy",
            6 when code == 'S' => "Variable local restrictions",
            _ => $"Unrecognised code ({code})"
        };
    }
}
