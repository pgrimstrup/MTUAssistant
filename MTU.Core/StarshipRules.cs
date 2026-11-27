using System.Globalization;
using System.Text.RegularExpressions;

namespace MTU.Core;

/// <summary>The kinds of starship system, in the order a ship's systems are listed. The numbers
/// are stored, so never renumber them.</summary>
public enum StarshipSystemType
{
    Hull = 0,
    Armour = 1,
    MDrive = 2,
    JDrive = 3,
    PowerPlant = 4,
    FuelTanks = 5,
    Bridge = 6,
    Computer = 7,
    Sensors = 8,
    System = 9,
    Staterooms = 10,
    Software = 11,
    CommonAreas = 12,
    Cargo = 13,
}

/// <summary>One crew position and how many of it, such as Engineer x3.</summary>
public sealed record StarshipCrewPosition(string Name, int Count);

/// <summary>What a ship can do, worked out from its systems.</summary>
/// <param name="HullPoints">Total Hull Points before damage.</param>
/// <param name="Thrust">M-Drive rating, in G.</param>
/// <param name="Jump">J-Drive rating: the longest jump, in parsecs.</param>
/// <param name="PowerOutput">Power Plant output.</param>
/// <param name="PowerRequired">Power needed by every system together.</param>
/// <param name="CargoTons">Tons given to Cargo.</param>
/// <param name="TonsAllocated">Tons taken up by every system together.</param>
public sealed record StarshipPerformance(
    int HullPoints,
    int Thrust,
    int Jump,
    decimal PowerOutput,
    decimal PowerRequired,
    decimal CargoTons,
    decimal TonsAllocated
);

public static partial class StarshipRules
{
    public const int MinTechLevel = 9;
    public const int MaxTechLevel = 25;
    public const int MinDisplacement = 100;
    public const int MaxDisplacement = 500_000;
    public const int MaxSystems = 200;
    public const int MaxRating = 1_000_000;
    public const decimal MaxCost = 1_000_000_000m;

    public static IReadOnlyList<StarshipSystemType> SystemTypes { get; } =
        Enum.GetValues<StarshipSystemType>();

    public static string Label(StarshipSystemType type) =>
        type switch
        {
            StarshipSystemType.MDrive => "M-Drive",
            StarshipSystemType.JDrive => "J-Drive",
            StarshipSystemType.PowerPlant => "Power Plant",
            StarshipSystemType.FuelTanks => "Fuel Tanks",
            StarshipSystemType.CommonAreas => "Common Areas",
            _ => type.ToString(),
        };

    /// <summary>What each kind of system's Rating means, for the dialog.</summary>
    public static string RatingHint(StarshipSystemType type) =>
        type switch
        {
            StarshipSystemType.Hull => "Hull Points",
            StarshipSystemType.Armour => "Armour",
            StarshipSystemType.MDrive => "Thrust (G)",
            StarshipSystemType.JDrive => "Jump (parsecs)",
            StarshipSystemType.PowerPlant => "Power output",
            StarshipSystemType.Computer => "Computer rating",
            _ => "",
        };

    /// <summary>Hull Points for a standard hull: one for every 2.5 tons, rounded down. Shown as a
    /// guide for the Hull system's Rating.</summary>
    // RULES-ASSUMPTION: High Guard's standard hull (displacement ÷ 2.5). Hull options such as
    // reinforced or light hulls change this, so the GM enters the hull's points as its Rating.
    public static int StandardHullPoints(int displacement) =>
        (int)Math.Floor(Math.Max(0, displacement) / 2.5m);

    /// <summary>The price of a standard design, as a share of its systems' cost.</summary>
    public const decimal StandardDesignShare = 0.9m;

    /// <summary>Purchase Cost in MCr: the systems' total cost, less 10% unless the ship is a
    /// prototype (a one-off design pays full price).</summary>
    public static decimal PurchaseCost(IEnumerable<StarshipSystem> systems, bool prototype) =>
        systems.Sum(s => s.Cost) * (prototype ? 1m : StandardDesignShare);

    /// <summary>Maintenance Cost in credits per month: the Purchase Cost in credits ÷ 12,000,
    /// rounded down.</summary>
    public static decimal MaintenanceCost(decimal purchaseCostMCr) =>
        Math.Floor(purchaseCostMCr * 1_000_000m / 12_000m);

    public static StarshipPerformance Performance(IEnumerable<StarshipSystem> systems)
    {
        var all = systems as IReadOnlyCollection<StarshipSystem> ?? systems.ToArray();
        // Ratings are at most MaxRating and a ship has at most MaxSystems, so sums fit an int.
        int Ratings(StarshipSystemType type) => all.Where(s => s.Type == type).Sum(s => s.Rating);
        int Best(StarshipSystemType type) =>
            all.Where(s => s.Type == type).Select(s => s.Rating).DefaultIfEmpty(0).Max();
        return new StarshipPerformance(
            // RULES-ASSUMPTION: each Hull system's Rating is its Hull Points, and they add up, so
            // a hull option can be entered as a second Hull system.
            Ratings(StarshipSystemType.Hull),
            // RULES-ASSUMPTION: a ship moves and jumps with its best drive; a second drive is a
            // spare and doesn't add to the first.
            Best(StarshipSystemType.MDrive),
            Best(StarshipSystemType.JDrive),
            // Power plants add together.
            all.Where(s => s.Type == StarshipSystemType.PowerPlant).Sum(s => (decimal)s.Rating),
            all.Sum(s => s.PowerRequirement),
            all.Where(s => s.Type == StarshipSystemType.Cargo).Sum(s => s.Displacement),
            all.Sum(s => s.Displacement)
        );
    }

    /// <summary>Reads crew text such as "Pilot, Engineer x3" into positions. A position without a
    /// multiplier counts once; "x3", "X3" and "×3" all mean three. Blank entries are skipped.</summary>
    public static IReadOnlyList<StarshipCrewPosition> ParseCrew(string? crew) =>
        (crew ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(entry =>
                Multiplier().Match(entry) is { Success: true } match
                && int.TryParse(
                    match.Groups[2].Value,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var count
                )
                && match.Groups[1].Value.Trim().Length > 0
                    ? new StarshipCrewPosition(match.Groups[1].Value.Trim(), count)
                    : new StarshipCrewPosition(entry, 1)
            )
            .ToArray();

    /// <summary>How many crew the positions need altogether.</summary>
    public static int CrewCount(string? crew) => ParseCrew(crew).Sum(p => p.Count);

    [GeneratedRegex(@"^(.*?)\s*[x×]\s*(\d{1,4})$", RegexOptions.IgnoreCase)]
    private static partial Regex Multiplier();
}
