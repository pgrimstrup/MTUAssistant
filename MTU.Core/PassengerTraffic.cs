namespace MTU.Core;

public sealed record PassengerLine(string Passage, int Roll, int PassageDm, int ModifiedRoll, int Dice, int Passengers, int Taken = 0);
public sealed record PassengerResult(int Effect, int Steward, int PopulationDm, int StarportDm, int ZoneDm,
    string Population, string Starport, string Zone, IReadOnlyList<PassengerLine> Lines, long? TargetDay = null);

/// <summary>Passenger traffic using the 2016 Mongoose Traveller 2e rules.</summary>
public static class PassengerTraffic
{
    public sealed record PassageFee(int Distance, int High, int Middle, int Basic, int Low)
    {
        public long Payment(PassengerResult? result) => result?.Lines.Sum(line => (long)line.Taken * (line.Passage switch {
            "High" => High, "Middle" => Middle, "Basic" => Basic, "Low" => Low, _ => 0
        })) ?? 0;
    }

    public static IReadOnlyList<PassageFee> Fees { get; } = Array.AsReadOnly<PassageFee>([
        new(1, 8500, 6200, 2200, 700), new(2, 12000, 9000, 2900, 1300),
        new(3, 20000, 15000, 4400, 2200), new(4, 41000, 31000, 8600, 4300),
        new(5, 45000, 34000, 9400, 13000), new(6, 470000, 350000, 93000, 96000)
    ]);

    public static PassengerResult Select(PassengerResult result, string passage, int count)
    {
        var line = result.Lines.SingleOrDefault(item => item.Passage == passage)
            ?? throw new ArgumentException("Unknown passage class.");
        if (count < 0 || count > line.Passengers)
            throw new ArgumentException($"Choose between 0 and {line.Passengers} {passage} passengers.");
        return result with { Lines = result.Lines.Select(item => item.Passage == passage ? item with { Taken = count } : item).ToArray() };
    }

    public static int DiceFor(int roll) => roll switch {
        <= 1 => 0, <= 3 => 1, <= 6 => 2, <= 10 => 3, <= 13 => 4,
        <= 15 => 5, 16 => 6, 17 => 7, 18 => 8, 19 => 9, _ => 10
    };

    public static PassengerResult Roll(string? uwp, string? zone, int effect, int steward, Func<int>? d6 = null)
    {
        var profile = uwp?.Trim().ToUpperInvariant() ?? "";
        const string digits = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        if (profile.Length != 9 || profile[7] != '-' || !"ABCDEX".Contains(profile[0]) || !digits.Contains(profile[4]))
            throw new ArgumentException("The world's starport or population is unknown.");
        if (effect is < -100 or > 100 || steward is < 0 or > 100)
            throw new ArgumentException("Enter an effect from -100 to 100 and a Steward skill from 0 to 100.");
        var population = digits.IndexOf(profile[4]);
        var populationDm = population <= 1 ? -4 : population <= 5 ? 0 : population <= 7 ? 1 : 3;
        var starportDm = profile[0] switch { 'A' => 2, 'B' => 1, 'E' => -1, 'X' => -3, _ => 0 };
        var zoneCode = zone?.Trim().ToUpperInvariant();
        var zoneDm = zoneCode switch { "A" or "AMBER" or "AMBERZONE" => 1, "R" or "RED" or "REDZONE" => -4, _ => 0 };
        var zoneLabel = zoneDm switch { 1 => "Amber", -4 => "Red", _ => "None" };
        d6 ??= () => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 7);
        var lines = new List<PassengerLine>();
        foreach (var (passage, passageDm) in new[] { ("High", -4), ("Middle", 0), ("Basic", 0), ("Low", 1) })
        {
            var roll = d6() + d6();
            var modified = roll + effect + steward + populationDm + starportDm + zoneDm + passageDm;
            var dice = DiceFor(modified);
            var passengers = Enumerable.Range(0, dice).Sum(_ => d6());
            lines.Add(new(passage, roll, passageDm, modified, dice, passengers));
        }
        return new(effect, steward, populationDm, starportDm, zoneDm, profile[4].ToString(), profile[0].ToString(), zoneLabel, lines);
    }
}
