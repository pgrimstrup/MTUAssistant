namespace MTU.Core;

public sealed record PopulationWorld(
    string Milieu,
    int SectorId,
    string Uwp,
    string Pbg,
    string AllegianceCode
);

public sealed record PopulationAllegiance(string Milieu, int SectorId, string Code, string Name);

public sealed record PolityPopulation(
    string Code,
    string Name,
    decimal Population,
    decimal SharePercent
);

public sealed record MilieuPopulationStatistics(
    string Milieu,
    decimal TotalPopulation,
    int KnownSystems,
    int UnknownSystems,
    IReadOnlyList<PolityPopulation> TopPolities,
    PolityPopulation Others,
    int OtherPolityCount
);

public static class SectorPopulation
{
    private const string Digits = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    private static string? FallbackName(string code) =>
        code switch
        {
            "NA" => "Non-aligned or Independent",
            "JU" => "Vassalry Judiciate",
            _ => null,
        };

    /// <summary>Second Survey: PBG multiplier times 10 to the UWP population exponent. Unknown values are excluded.</summary>
    public static decimal? WorldPopulation(string? uwp, string? pbg)
    {
        var profile = uwp?.Trim().ToUpperInvariant() ?? "";
        var pbgValue = pbg?.Trim().ToUpperInvariant() ?? "";
        if (profile.Length != 9 || profile[7] != '-' || pbgValue.Length == 0)
            return null;
        var exponent = Digits.IndexOf(profile[4]);
        var multiplier = Digits.IndexOf(pbgValue[0]);
        if (exponent is < 0 or > 15 || multiplier < 0)
            return null;
        // Traveller Map documents this correction for legacy populated worlds with multiplier zero.
        if (multiplier == 0 && exponent > 0)
            multiplier = 1;
        decimal magnitude = 1;
        for (var i = 0; i < exponent; i++)
            magnitude *= 10;
        return multiplier * magnitude;
    }

    public static IReadOnlyList<MilieuPopulationStatistics> Calculate(
        IEnumerable<string> milieus,
        IEnumerable<PopulationWorld> worlds,
        IEnumerable<PopulationAllegiance> allegiances
    )
    {
        static string Prefix(string code)
        {
            var trimmed = code.Trim();
            if (trimmed.Length == 0 || trimmed == "-" || trimmed.All(c => c == '?'))
                return "";
            return trimmed[..Math.Min(2, trimmed.Length)].ToUpperInvariant();
        }
        static string DisplayCode(string code) =>
            code.Length == 2 ? $"{code[0]}{char.ToLowerInvariant(code[1])}" : code;
        var names = allegiances
            .Where(a => !string.IsNullOrWhiteSpace(a.Code) && !string.IsNullOrWhiteSpace(a.Name))
            .GroupBy(a => (a.Milieu, Code: Prefix(a.Code)))
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var rootNames = g.Where(a => a.Code.Trim().Length <= 2)
                        .Select(a => a.Name.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    if (rootNames.Length == 1)
                        return rootNames[0];
                    if (rootNames.Length > 1)
                        return FallbackName(g.Key.Code) ?? DisplayCode(g.Key.Code);
                    var familyNames = g.Select(a => a.Name.Split(',', '(')[0].Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    return FallbackName(g.Key.Code)
                        ?? (familyNames.Length == 1 ? familyNames[0] : DisplayCode(g.Key.Code));
                }
            );
        var worldsByMilieu = worlds.ToLookup(w => w.Milieu);
        return milieus
            .Distinct()
            .OrderBy(m => m, StringComparer.Ordinal)
            .Select(milieu =>
            {
                var totals = new Dictionary<string, (string Name, decimal Population)>();
                var known = 0;
                var unknown = 0;
                foreach (var world in worldsByMilieu[milieu])
                {
                    var population = WorldPopulation(world.Uwp, world.Pbg);
                    if (population is null)
                    {
                        unknown++;
                        continue;
                    }
                    known++;
                    var code = Prefix(world.AllegianceCode);
                    var name =
                        code.Length == 0 ? "Unspecified polity"
                        : code == "IM" ? "Third Imperium"
                        : names.GetValueOrDefault((milieu, code))
                            ?? FallbackName(code)
                            ?? DisplayCode(code);
                    var current = totals.GetValueOrDefault(code, (Name: name, Population: 0m));
                    totals[code] = (current.Name, current.Population + population.Value);
                }
                var total = totals.Values.Sum(p => p.Population);
                decimal Share(decimal population) => total == 0 ? 0 : population / total * 100;
                var ranked = totals
                    .Select(p => new PolityPopulation(
                        DisplayCode(p.Key),
                        p.Value.Name,
                        p.Value.Population,
                        Share(p.Value.Population)
                    ))
                    .OrderByDescending(p => p.Population)
                    .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.Code, StringComparer.Ordinal)
                    .ToArray();
                var others = ranked.Skip(10).Sum(p => p.Population);
                return new MilieuPopulationStatistics(
                    milieu,
                    total,
                    known,
                    unknown,
                    ranked.Take(10).ToArray(),
                    new("", "Others", others, Share(others)),
                    Math.Max(0, ranked.Length - 10)
                );
            })
            .ToArray();
    }
}
