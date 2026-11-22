namespace MTU.Core;

/// <summary>One four-year term of a character's career. Number is its position, starting at 1.
/// The checks record whether each roll succeeded. Commission only applies to military services.</summary>
public sealed record CareerTerm(
    int Id,
    int CharacterId,
    int Number,
    string Service,
    bool Survival = true,
    bool Advancement = false,
    bool Commission = false,
    string Notes = ""
);

/// <summary>A character's standing in one service. Officer is null until a Commission Check
/// succeeds. Highest is the larger of the enlisted and officer numbers, not their sum.</summary>
public sealed record CareerRank(
    string Service,
    int Terms,
    int Enlisted,
    int? Officer,
    bool Military
)
{
    public int Highest => Math.Max(Enlisted, Officer ?? 0);

    /// <summary>"O-2" or "E-3" for military services, "Rank 2" for the others.</summary>
    public string Label =>
        !Military ? $"Rank {Enlisted}"
        : Officer is int officer && officer >= Enlisted ? OfficerLabel(officer)
        : EnlistedLabel(Enlisted);

    public static string OfficerLabel(int rank) => $"O-{rank}";

    public static string EnlistedLabel(int rank) => $"E-{rank}";
}

public static class CareerServices
{
    public const string Other = "Other";

    /// <summary>The services offered for a term. Any other name is entered through Other.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        "Agent",
        "Army",
        "Citizen",
        "Drifter",
        "Entertainer",
        "Marine",
        "Merchant",
        "Military Academy",
        "Navy",
        "Noble",
        "Rogue",
        "Scholar",
        "Scout",
        "University",
        "Prisoner",
    ];

    /// <summary>Services with enlisted and officer ranks and a Commission Check.</summary>
    public static IReadOnlyList<string> Military { get; } =
    ["Army", "Marine", "Navy", "Military Academy"];

    public static bool IsMilitary(string? service) =>
        Military.Contains(service?.Trim() ?? "", StringComparer.OrdinalIgnoreCase);

    /// <summary>Services with no Advancement Check: study at University or Military Academy.</summary>
    public static IReadOnlyList<string> WithoutAdvancement { get; } =
    ["University", "Military Academy"];

    public static bool HasAdvancement(string? service) =>
        !WithoutAdvancement.Contains(service?.Trim() ?? "", StringComparer.OrdinalIgnoreCase);

    /// <summary>The listed spelling of a service, or the trimmed name for an unlisted one.</summary>
    public static string Normalize(string? service)
    {
        var name = service?.Trim() ?? "";
        return All.FirstOrDefault(s => s.Equals(name, StringComparison.OrdinalIgnoreCase)) ?? name;
    }
}

public static class CareerRules
{
    public const int StartingAge = 16;
    public const int YearsPerTerm = 4;

    /// <summary>Age when the given term (1-based) begins.</summary>
    public static int AgeAtStart(int termNumber) =>
        StartingAge + YearsPerTerm * Math.Max(0, termNumber - 1);

    /// <summary>Standing in one service. Each successful Advancement Check adds one rank.
    /// Military rank is carried between the military services: recruits start at E-0, a successful
    /// Commission Check ends enlisted advancement and starts officer rank at O-1, and a character
    /// who moves from one military service to another keeps the rank they hold. The standing in a
    /// military service is the rank held at the end of the last term served in it. Other services
    /// each keep their own rank, starting at 0, counted over that service's terms.</summary>
    public static CareerRank Rank(IEnumerable<CareerTerm> terms, string service)
    {
        var name = CareerServices.Normalize(service);
        var military = CareerServices.IsMilitary(name);
        int count = 0,
            enlisted = 0,
            heldEnlisted = 0;
        int? officer = null,
            heldOfficer = null;
        foreach (var term in terms.OrderBy(t => t.Number).ThenBy(t => t.Id))
        {
            var termService = CareerServices.Normalize(term.Service);
            var sameService = termService.Equals(name, StringComparison.OrdinalIgnoreCase);
            // Every military term moves the shared military rank; other ranks only move in their own service.
            if (military ? !CareerServices.IsMilitary(termService) : !sameService)
                continue;
            // The Commission Check comes first in a term. If it passes there is no Advancement
            // Check that term: a character cannot be promoted twice in one term.
            if (military && term.Commission && officer is null)
                officer = 1;
            else if (term.Advancement && CareerServices.HasAdvancement(termService))
            {
                if (officer is int rank)
                    officer = rank + 1;
                else
                    enlisted++;
            }
            if (!sameService)
                continue;
            count++;
            (heldEnlisted, heldOfficer) = (enlisted, officer);
        }
        return new(name, count, heldEnlisted, heldOfficer, military);
    }

    /// <summary>Whether this term can take a Commission Check: only in a military service, and
    /// only while no other military term already holds the character's commission. A character
    /// is commissioned once in their whole military career.</summary>
    public static bool CommissionAvailable(
        IEnumerable<CareerTerm> terms,
        int termId,
        string? service
    ) =>
        CareerServices.IsMilitary(service)
        && !terms.Any(t => t.Id != termId && t.Commission && CareerServices.IsMilitary(t.Service));

    /// <summary>The ranks to display, e.g. "Military Academy O-1", "Navy O-2", "Scout Rank 1":
    /// every service where the character holds a rank above 0.</summary>
    public static IReadOnlyList<string> RankLabels(IEnumerable<CareerTerm>? terms) =>
        Ranks(terms ?? [])
            .Where(r => r.Highest > 0)
            .Select(r => $"{r.Service} {r.Label}")
            .ToArray();

    /// <summary>Standing in every service the character has served in, in order of first term.</summary>
    public static IReadOnlyList<CareerRank> Ranks(IEnumerable<CareerTerm> terms)
    {
        var all = terms.OrderBy(t => t.Number).ThenBy(t => t.Id).ToArray();
        return all.Select(t => CareerServices.Normalize(t.Service))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(service => Rank(all, service))
            .ToArray();
    }

    /// <summary>The highest rank number in a service, 0 when the character never served in it.</summary>
    public static int HighestRank(IEnumerable<CareerTerm>? terms, string service) =>
        Rank(terms ?? [], service).Highest;

    /// <summary>Standing after each term, keyed by term ID, for showing rank progress.</summary>
    public static IReadOnlyDictionary<int, CareerRank> RankAfterEachTerm(
        IEnumerable<CareerTerm> terms
    )
    {
        var all = terms.OrderBy(t => t.Number).ThenBy(t => t.Id).ToArray();
        var result = new Dictionary<int, CareerRank>();
        for (var i = 0; i < all.Length; i++)
            result[all[i].Id] = Rank(all.Take(i + 1), all[i].Service);
        return result;
    }

    /// <summary>What each term did to the character's rank, keyed by term ID: "Commissioned to
    /// O-1" for the term that holds the commission, "Promoted to E-2", "Promoted to O-3" or
    /// "Promoted to Rank 1" for a passed Advancement Check, and nothing for a term with neither.
    /// A promotion names the rank just gained, so an officer below their old enlisted number is
    /// still "Promoted to O-2".</summary>
    public static IReadOnlyDictionary<int, string> PromotionInEachTerm(
        IEnumerable<CareerTerm> terms
    )
    {
        var all = terms.OrderBy(t => t.Number).ThenBy(t => t.Id).ToArray();
        var result = new Dictionary<int, string>();
        var commissioned = false;
        for (var i = 0; i < all.Length; i++)
        {
            var term = all[i];
            var military = CareerServices.IsMilitary(term.Service);
            if (military && term.Commission && !commissioned)
            {
                commissioned = true;
                result[term.Id] = $"Commissioned to {CareerRank.OfficerLabel(1)}";
            }
            else if (term.Advancement && CareerServices.HasAdvancement(term.Service))
            {
                var rank = Rank(all.Take(i + 1), term.Service);
                var gained =
                    !military ? rank.Label
                    : rank.Officer is int officer ? CareerRank.OfficerLabel(officer)
                    : CareerRank.EnlistedLabel(rank.Enlisted);
                result[term.Id] = $"Promoted to {gained}";
            }
        }
        return result;
    }

    /// <summary>"Navy 3, Scout 1": terms per service in order of first term.</summary>
    public static string Summary(IEnumerable<CareerTerm>? terms) =>
        string.Join(", ", Ranks(terms ?? []).Select(r => $"{r.Service} {r.Terms}"));
}
