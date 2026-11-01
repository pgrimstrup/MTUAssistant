using System.Text.RegularExpressions;

namespace MTU.Core;

public sealed record TradeCode(string Code, string Name, string Description, string Category);

/// <summary>Trade classifications from the stored Remarks field; edit descriptions here.</summary>
/// <remarks>Code reference: https://travellermap.com/doc/secondsurvey#remarks.</remarks>
public static partial class TradeCodeLookup
{
    private static readonly TradeCode[] Definitions = [
        new("As", "Asteroid belt", "The main world is a belt of asteroids.", "physical"),
        new("De", "Desert", "A dry world with no surface water.", "physical"),
        new("Fl", "Fluid oceans", "Surface seas contain fluids other than water.", "physical"),
        new("Ga", "Garden", "A temperate world with a hospitable atmosphere and surface water.", "physical"),
        new("He", "Hellworld", "A hostile, dry world with an extreme atmosphere.", "physical"),
        new("Ic", "Ice-capped", "Surface water is frozen beneath a thin atmosphere or vacuum.", "physical"),
        new("Oc", "Ocean world", "A large world covered by oceans.", "physical"),
        new("Va", "Vacuum", "No atmosphere is present.", "physical"),
        new("Wa", "Water world", "Nearly the entire surface is covered by water.", "physical"),
        new("Di", "Dieback", "Formerly inhabited; surviving technology outlasts its population.", "population"),
        new("Ba", "Barren", "Uninhabited, with no established government or technology.", "population"),
        new("Lo", "Low population", "A population numbering in the tens, hundreds or thousands.", "population"),
        new("Ni", "Non-industrial", "The population is too small to support a broad industrial base.", "population"),
        new("Ph", "Pre-high population", "A population in the hundreds of millions.", "population"),
        new("Hi", "High population", "A population of at least one billion.", "population"),
        new("Pa", "Pre-agricultural", "Suitable growing conditions, with population outside the agricultural classification.", "economic"),
        new("Ag", "Agricultural", "A habitable world supporting substantial agricultural production.", "economic"),
        new("Na", "Non-agricultural", "A populated world with little atmosphere or surface water for farming.", "economic"),
        new("Pi", "Pre-industrial", "Environmental conditions suit industry, but the population is below industrial levels.", "economic"),
        new("In", "Industrial", "A densely populated world with conditions supporting extensive industry.", "economic"),
        new("Po", "Poor", "Limited atmosphere and water constrain economic development.", "economic"),
        new("Pr", "Pre-rich", "Favourable atmosphere and government, with population outside the rich classification.", "economic"),
        new("Ri", "Rich", "Favourable atmosphere, population and government support a prosperous economy.", "economic"),
        new("Lt", "Low technology", "Local technology is level 5 or below.", "economic"),
        new("Ht", "High technology", "Local technology is level 12 or above.", "economic"),
        new("Fr", "Frozen", "A cold world beyond the habitable zone.", "physical"),
        new("Ho", "Hot", "A world on the hot side of the habitable zone.", "physical"),
        new("Co", "Cold", "A world on the cold side of the habitable zone.", "physical"),
        new("Lk", "Locked", "A tidally locked close satellite.", "physical"),
        new("Tr", "Tropic", "A warm world near the inner edge of the habitable zone.", "physical"),
        new("Tu", "Tundra", "A cold world near the outer edge of the habitable zone.", "physical"),
        new("Tz", "Twilight zone", "A world in a very close orbit.", "physical"),
        new("Fa", "Farming", "A secondary world providing agricultural production.", "economic"),
        new("Mi", "Mining", "A secondary world supporting mineral extraction.", "economic"),
        new("Mr", "Military rule", "Governed by a regional military power.", "political"),
        new("Px", "Prison or exile camp", "The main world is used for imprisonment or exile.", "special"),
        new("Pe", "Penal colony", "A secondary world used as a penal settlement.", "special"),
        new("Re", "Reserve", "A world designated as a reserve.", "special"),
        new("Cp", "Subsector capital", "The administrative capital of its subsector.", "political"),
        new("Cs", "Sector capital", "The administrative capital of its sector.", "political"),
        new("Cx", "Capital", "A major political capital.", "political"),
        new("Cy", "Colony", "A colony administered by another world.", "political"),
        new("Sa", "Satellite", "The main world is a moon of a gas giant.", "physical"),
        new("Fo", "Forbidden", "Access is forbidden; designated a red travel zone.", "special"),
        new("Pz", "Puzzle", "An amber-zone world with unusual, unresolved conditions.", "special"),
        new("Da", "Danger", "An amber-zone world presenting hazards to visitors.", "special"),
        new("Ab", "Data repository", "A repository of stored information.", "special"),
        new("An", "Ancient site", "A site associated with the Ancients.", "special"),
        new("Rs", "Research station", "An Imperial research installation is present.", "special")
    ];
    private static readonly IReadOnlyDictionary<string, TradeCode> Codes =
        Definitions.ToDictionary(code => code.Code, StringComparer.OrdinalIgnoreCase);

    // Preserve grouped remarks so species, ownership and free-form notes are not trade codes.
    [GeneratedRegex(@"\{[^}]*\}|\([^)]*\)\d*|\[[^\]]*\]|\S+")]
    private static partial Regex RemarkTokens();

    public static IReadOnlyList<TradeCode> Describe(string? remarks)
    {
        var result = new List<TradeCode>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in RemarkTokens().Matches(remarks ?? ""))
        {
            var token = match.Value;
            TradeCode? code = Codes.GetValueOrDefault(token);
            if (code is null && token.Length == 3 && token.StartsWith("Rs", StringComparison.OrdinalIgnoreCase)
                && char.IsAsciiLetter(token[2]))
                code = new($"Rs{char.ToUpperInvariant(token[2])}", "Research station", $"Imperial research station {char.ToUpperInvariant(token[2])}.", "special");
            if (code is null && token.Length == 2 && token.All(char.IsAsciiLetter))
                code = new(token, "Unrecognised trade code", "No description is configured for this code.", "special");
            if (code is not null && seen.Add(code.Code)) result.Add(code);
        }
        return result;
    }
}
