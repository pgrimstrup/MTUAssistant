namespace MTU.Core;

public sealed record FreightLine(string Type, int Roll, int TypeDm, int ModifiedRoll, int Dice, int Lots, string LotSize, int? CargoTaken = null);
public sealed record FreightWorldModifiers(int PopulationDm, int StarportDm, int TechDm, int ZoneDm,
    string Population, string Starport, string Tech, string Zone);
public sealed record FreightResult(int Effect, int PopulationDm, int StarportDm, int TechDm, int ZoneDm,
    string Population, string Starport, string Tech, string Zone, IReadOnlyList<FreightLine> Lines, int? CargoCapacity = null, MailResult? Mail = null, long? TargetDay = null,
    FreightWorldModifiers? Destination = null, int Distance = 0, int? DestinationSectorId = null, string? DestinationHex = null);

public sealed record MailResult(int Rank, int SocDm, int FreightTrafficDm, int Effect, int TechDm,
    int Roll, int ModifiedRoll, int Containers, int? CargoTaken = null, bool InsufficientSpace = false, int ArmedDm = 0);

/// <summary>Freight traffic and route modifiers.</summary>
public static class FreightTraffic
{
    public static int DiceFor(int roll) => roll switch {
        <= 1 => 0, <= 3 => 1, <= 5 => 2, <= 8 => 3, <= 11 => 4,
        <= 14 => 5, <= 16 => 6, 17 => 7, 18 => 8, 19 => 9, _ => 10
    };

    public static FreightResult TakeCargo(FreightResult freight, int capacity, Func<int>? d6 = null)
    {
        if (capacity < 0) throw new ArgumentException("Cargo capacity must be zero or greater.");
        d6 ??= () => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 7);
        var mailTaken = freight.Mail?.CargoTaken ?? 0;
        if (mailTaken > capacity) throw new ArgumentException("Cargo capacity is less than the mail already taken.");
        var remaining = capacity - mailTaken;
        var loaded = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (type, multiplier) in new[] { ("Major", 10), ("Minor", 5), ("Incidental", 1) })
        {
            var line = freight.Lines.FirstOrDefault(item => item.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            var taken = 0;
            for (var i = 0; i < (line?.Lots ?? 0) && remaining > 0; i++)
            {
                var size = d6() * multiplier;
                if (size > remaining) continue;
                taken += size;
                remaining -= size;
            }
            loaded[type] = taken;
        }
        return freight with { CargoCapacity = capacity, Lines = freight.Lines.Select(line =>
            line with { CargoTaken = loaded.GetValueOrDefault(line.Type) }).ToArray() };
    }

    public static int MailTrafficDm(int freightDm) => freightDm switch {
        <= -10 => -2, <= -5 => -1, <= 4 => 0, <= 9 => 1, _ => 2
    };

    public static FreightResult RollMail(FreightResult freight, int rank, int socDm, Func<int>? d6 = null, bool armed = true)
    {
        if (rank is < 0 or > 100 || socDm is < -100 or > 100)
            throw new ArgumentException("Enter a rank from 0 to 100 and a SOC DM from -100 to 100.");
        d6 ??= () => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 7);
        var destination = freight.Destination;
        var freightDm = freight.Effect + freight.PopulationDm + freight.StarportDm + freight.TechDm + freight.ZoneDm
            + (destination?.PopulationDm ?? 0) + (destination?.StarportDm ?? 0) + (destination?.TechDm ?? 0) + (destination?.ZoneDm ?? 0)
            + (freight.Distance > 0 ? 1 - freight.Distance : 0);
        var armedDm = armed ? 2 : 0;
        var effect = MailTrafficDm(freightDm) + rank + socDm + armedDm;
        var techDm = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ".IndexOf(freight.Tech, StringComparison.Ordinal) <= 5 ? -4 : 0;
        var roll = d6() + d6();
        var modified = roll + effect + techDm;
        var containers = modified >= 12 ? d6() : 0;
        return freight with { Mail = new(rank, socDm, freightDm, effect, techDm, roll, modified, containers, ArmedDm: armedDm) };
    }

    public static FreightResult TakeMail(FreightResult freight, int capacity)
    {
        if (capacity < 0) throw new ArgumentException("Cargo capacity must be zero or greater.");
        if (freight.Mail is not { Containers: > 0 } mail) throw new ArgumentException("There is no mail available to take.");
        var cargo = freight.Lines.Sum(line => line.CargoTaken ?? 0);
        var tons = mail.Containers * 5;
        if ((long)cargo + tons > capacity)
            return freight with { Mail = mail with { CargoTaken = 0, InsufficientSpace = true } };
        return freight with { CargoCapacity = capacity, Mail = mail with { CargoTaken = tons, InsufficientSpace = false } };
    }

    public static FreightResult Roll(string? uwp, string? zone, int effect, Func<int>? d6 = null) =>
        RollCore(uwp, zone, effect, null, 0, null, null, d6);

    public static FreightResult RollRoute(string? uwp, string? zone, string? destinationUwp, string? destinationZone,
        int distance, int destinationSectorId, string destinationHex, int effect, Func<int>? d6 = null)
    {
        if (distance is < 1 or > 6) throw new ArgumentException("Destination must be between 1 and 6 parsecs away.");
        var destination = WorldModifiers(destinationUwp, destinationZone);
        return RollCore(uwp, zone, effect, destination, distance, destinationSectorId, destinationHex, d6);
    }

    private static FreightWorldModifiers WorldModifiers(string? uwp, string? zone)
    {
        var profile = uwp?.Trim().ToUpperInvariant() ?? "";
        const string digits = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        if (profile.Length != 9 || profile[7] != '-' || !"ABCDEX".Contains(profile[0])
            || !digits.Contains(profile[4]) || !digits.Contains(profile[8]))
            throw new ArgumentException("The world's starport, population or tech level is unknown.");
        var population = digits.IndexOf(profile[4]);
        var populationDm = population <= 1 ? -4 : population <= 5 ? 0 : population <= 7 ? 2 : 4;
        var starportDm = profile[0] switch { 'A' => 2, 'B' => 1, 'E' => -1, 'X' => -3, _ => 0 };
        var tech = digits.IndexOf(profile[8]);
        var techDm = tech <= 6 ? -1 : tech >= 9 ? 2 : 0;
        var zoneCode = zone?.Trim().ToUpperInvariant();
        var zoneDm = zoneCode switch { "A" or "AMBER" or "AMBERZONE" => -2, "R" or "RED" or "REDZONE" => -6, _ => 0 };
        var zoneLabel = zoneDm switch { -2 => "Amber", -6 => "Red", _ => "None" };
        return new(populationDm, starportDm, techDm, zoneDm, profile[4].ToString(), profile[0].ToString(), profile[8].ToString(), zoneLabel);
    }

    private static FreightResult RollCore(string? uwp, string? zone, int effect, FreightWorldModifiers? destination,
        int distance, int? destinationSectorId, string? destinationHex, Func<int>? d6)
    {
        if (effect is < -100 or > 100) throw new ArgumentException("Enter an effect from -100 to 100.");
        var origin = WorldModifiers(uwp, zone);
        var distanceDm = distance > 0 ? 1 - distance : 0;
        d6 ??= () => System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 7);
        var lines = new List<FreightLine>();
        foreach (var (type, typeDm, lotSize) in new[] { ("Major", -4, "1Dx10 (10 to 60)"),
            ("Minor", 0, "1Dx5 (5 to 30)"), ("Incidental", 2, "1D (1 to 6)") })
        {
            var roll = d6() + d6();
            var modified = roll + effect + origin.PopulationDm + origin.StarportDm + origin.TechDm + origin.ZoneDm
                + (destination?.PopulationDm ?? 0) + (destination?.StarportDm ?? 0) + (destination?.TechDm ?? 0)
                + (destination?.ZoneDm ?? 0) + distanceDm + typeDm;
            var dice = DiceFor(modified);
            var lots = Enumerable.Range(0, dice).Sum(_ => d6());
            lines.Add(new(type, roll, typeDm, modified, dice, lots, lotSize));
        }
        return new(effect, origin.PopulationDm, origin.StarportDm, origin.TechDm, origin.ZoneDm, origin.Population,
            origin.Starport, origin.Tech, origin.Zone, lines, Destination: destination, Distance: distance,
            DestinationSectorId: destinationSectorId, DestinationHex: destinationHex);
    }
}
