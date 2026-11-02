namespace MTU.Core;

public sealed record PassengerLine(string Passage, int Roll, int PassageDm, int ModifiedRoll, int Dice, int Passengers, int Taken = 0, bool Available = true);
public sealed record PassengerWorldModifiers(int PopulationDm, int StarportDm, int ZoneDm, string Population, string Starport, string Zone);
public sealed record PassengerResult(int Effect, int Steward, int PopulationDm, int StarportDm, int ZoneDm,
    string Population, string Starport, string Zone, IReadOnlyList<PassengerLine> Lines, long? TargetDay = null,
    PassengerWorldModifiers? Destination = null, int Distance = 0, int? DestinationSectorId = null, string? DestinationHex = null, bool HasSteward = true);

/// <summary>Passenger traffic and route modifiers.</summary>
public static class PassengerTraffic
{
    public sealed record PassageFee(int Distance, int High, int Middle, int Basic, int Low)
    {
        public long Payment(PassengerResult? result) => result?.Lines.Sum(line => (long)line.Taken * (line.Passage switch {
            "High" => High, "Middle" => Middle, "Basic" => Basic, "Low" => Low, _ => 0
        })) ?? 0;
    }

    public static IReadOnlyList<PassageFee> Fees { get; } = Array.AsReadOnly<PassageFee>([
        new(1, 9000, 6500, 2000, 700), new(2, 14000, 10000, 3000, 1300),
        new(3, 21000, 14000, 5000, 2200), new(4, 34000, 23000, 8000, 3900),
        new(5, 60000, 40000, 14000, 7200), new(6, 210000, 130000, 55000, 27000)
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

    public static PassengerResult Roll(string? uwp, string? zone, int effect, int steward, Func<int>? d6 = null) =>
        RollCore(uwp, zone, effect, steward, null, 0, null, null, d6, true);

    public static PassengerResult RollRoute(string? uwp, string? zone, string? destinationUwp, string? destinationZone,
        int distance, int destinationSectorId, string destinationHex, int effect, int steward, Func<int>? d6 = null, bool hasSteward = true)
    {
        if (distance is < 1 or > 6) throw new ArgumentException("Destination must be between 1 and 6 parsecs away.");
        var destination = WorldModifiers(destinationUwp, destinationZone);
        return RollCore(uwp, zone, effect, steward, destination, distance, destinationSectorId, destinationHex, d6, hasSteward);
    }

    private static PassengerWorldModifiers WorldModifiers(string? uwp, string? zone)
    {
        var profile = uwp?.Trim().ToUpperInvariant() ?? "";
        const string digits = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        if (profile.Length != 9 || profile[7] != '-' || !"ABCDEX".Contains(profile[0]) || !digits.Contains(profile[4]))
            throw new ArgumentException("The world's starport or population is unknown.");
        var population = digits.IndexOf(profile[4]);
        var populationDm = population <= 1 ? -4 : population <= 5 ? 0 : population <= 7 ? 1 : 3;
        var starportDm = profile[0] switch { 'A' => 2, 'B' => 1, 'E' => -1, 'X' => -3, _ => 0 };
        var zoneCode = zone?.Trim().ToUpperInvariant();
        var zoneDm = zoneCode switch { "A" or "AMBER" or "AMBERZONE" => 1, "R" or "RED" or "REDZONE" => -4, _ => 0 };
        var zoneLabel = zoneDm switch { 1 => "Amber", -4 => "Red", _ => "None" };
        return new(populationDm, starportDm, zoneDm, profile[4].ToString(), profile[0].ToString(), zoneLabel);
    }

    private static PassengerResult RollCore(string? uwp, string? zone, int effect, int steward,
        PassengerWorldModifiers? destination, int distance, int? destinationSectorId, string? destinationHex, Func<int>? d6, bool hasSteward)
    {
        if (effect is < -100 or > 100)
            throw new ArgumentException("Enter an effect from -100 to 100.");
        var origin = WorldModifiers(uwp, zone);
        var distanceDm = distance > 0 ? 1 - distance : 0;
        d6 ??= () => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 7);
        var lines = new List<PassengerLine>();
        foreach (var (passage, passageDm) in new[] { ("High", -4), ("Middle", 0), ("Basic", 0), ("Low", 1) })
        {
            if (passage == "High" && !hasSteward)
            {
                lines.Add(new(passage, 0, passageDm, 0, 0, 0, Available: false));
                continue;
            }
            var roll = d6() + d6();
            var modified = roll + effect + steward + origin.PopulationDm + origin.StarportDm + origin.ZoneDm + (destination?.PopulationDm ?? 0) + (destination?.StarportDm ?? 0) + (destination?.ZoneDm ?? 0) + distanceDm + passageDm;
            var dice = DiceFor(modified);
            var passengers = Enumerable.Range(0, dice).Sum(_ => d6());
            lines.Add(new(passage, roll, passageDm, modified, dice, passengers));
        }
        return new(effect, steward, origin.PopulationDm, origin.StarportDm, origin.ZoneDm, origin.Population, origin.Starport, origin.Zone, lines, Destination: destination, Distance: distance, DestinationSectorId: destinationSectorId, DestinationHex: destinationHex, HasSteward: hasSteward);
    }
}
