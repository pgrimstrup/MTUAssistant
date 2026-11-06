using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;
using MTU.Core.Models;

await InstanceNameChecks.RunAsync();
await FogChecks.RunAsync();
await CoverProtectionChecks.RunAsync();
await WeaponSkillChecks.RunAsync();
WeaponAttackChecks.Run();
AttackResolutionChecks.Run();
CombatStatusChecks.Run();
await InjuryToleranceChecks.RunAsync();
await SpeedChecks.RunAsync();
await AutomaticMovementChecks.RunAsync();
AutoAttackChecks.Run();
await SkillChecks.RunAsync();
await NpcGenerationChecks.RunAsync();
await SceneChecks.RunAsync();
await AnimalCombatChecks.RunAsync();
await AnimalGenerationChecks.RunAsync();
await SharedAnimalChecks.RunAsync();
await CombatDescriptionChecks.RunAsync();

static void Check(bool condition, string message)
{
    if (!condition)
        throw new Exception(message);
}

Check(
    SectorPopulation.WorldPopulation("A788899-C", "703") == 700_000_000m,
    "Population uses PBG multiplier and UWP exponent"
);
Check(
    SectorPopulation.WorldPopulation("A000000-0", "000") == 0
        && SectorPopulation.WorldPopulation("A000800-0", "000") == 100_000_000m,
    "Zero population and legacy zero multiplier are handled correctly"
);
Check(
    SectorPopulation.WorldPopulation("A000?00-0", "703") is null
        && SectorPopulation.WorldPopulation("A000800-0", "?03") is null
        && SectorPopulation.WorldPopulation("A000800-0", "") is null
        && SectorPopulation.WorldPopulation("malformed", "703") is null,
    "Unknown population fields are excluded instead of guessed"
);
var populationFixtureWorlds = new List<PopulationWorld>();
var populationFixtureAllegiances = new List<PopulationAllegiance>();
for (var i = 1; i <= 12; i++)
{
    populationFixtureWorlds.Add(
        new("M1105", 1, "A000600-0", "0123456789ABC"[i] + "00", $"{(char)('A' + i - 1)}a01")
    );
    populationFixtureAllegiances.Add(
        new("M1105", 1, $"{(char)('A' + i - 1)}a01", $"Polity {i:00}")
    );
}
populationFixtureWorlds.Add(new("M1105", 2, "A000600-0", "200", "la99"));
populationFixtureWorlds.Add(new("M1105", 1, "A000000-0", "000", "ZER0"));
populationFixtureWorlds.Add(new("M1105", 1, "A000?00-0", "703", "Aa01"));
populationFixtureWorlds.Add(new("M1120", 3, "A000900-0", "900", "La01"));
var populationFixture = SectorPopulation.Calculate(
    ["M1105", "M1120"],
    populationFixtureWorlds,
    populationFixtureAllegiances
);
var presentPopulation = populationFixture.Single(p => p.Milieu == "M1105");
Check(
    presentPopulation.TotalPopulation == 80_000_000m
        && presentPopulation.KnownSystems == 14
        && presentPopulation.UnknownSystems == 1,
    "Population totals separate milieus and count unknown worlds"
);
Check(
    presentPopulation.TopPolities.Count == 10
        && presentPopulation.TopPolities[0]
            is { Code: "La", Name: "Polity 12", Population: 14_000_000m }
        && presentPopulation.Others.Population == 3_000_000m
        && presentPopulation.OtherPolityCount == 3
        && presentPopulation.TopPolities.Sum(p => p.Population)
            + presentPopulation.Others.Population
            == presentPopulation.TotalPopulation,
    "Top ten and Others reconcile with total; repeated polity codes aggregate across sectors"
);
Check(
    Math.Abs(
        presentPopulation.TopPolities.Sum(p => p.SharePercent)
            + presentPopulation.Others.SharePercent
            - 100
    ) < 0.00001m,
    "Population shares reconcile with total"
);
Check(
    populationFixture.Single(p => p.Milieu == "M1120").TotalPopulation == 9_000_000_000m,
    "Other eras are not double-counted"
);
var legacyPolities = SectorPopulation
    .Calculate(
        ["M1105"],
        [new("M1105", 1, "A000100-0", "100", "XX"), new("M1105", 2, "A000100-0", "100", "XX")],
        [new("M1105", 1, "XX", "State A"), new("M1105", 2, "XX", "State B")]
    )
    .Single();
Check(
    legacyPolities.TopPolities.Count == 1
        && legacyPolities.TopPolities[0] is { Code: "Xx", Population: 20 },
    "Matching two-letter prefixes aggregate even when recorded allegiance names differ"
);
var imperialPopulation = SectorPopulation
    .Calculate(
        ["M1105"],
        [
            new("M1105", 1, "A000600-0", "300", "ImDd"),
            new("M1105", 2, "A000600-0", "200", "imDa"),
            new("M1105", 3, "A000600-0", "100", "Im"),
        ],
        [
            new("M1105", 1, "ImDd", "Third Imperium, Domain of Deneb"),
            new("M1105", 2, "ImDa", "Third Imperium, Domain of Antares"),
        ]
    )
    .Single();
Check(
    imperialPopulation.TopPolities.Single()
        is { Code: "Im", Name: "Third Imperium", Population: 6_000_000m, SharePercent: 100 }
        && imperialPopulation.Others.Population == 0
        && imperialPopulation.OtherPolityCount == 0,
    "All Im allegiance variants and the legacy Im code sum into Third Imperium"
);
var namedMajorPolity = SectorPopulation
    .Calculate(
        ["M1105"],
        [new("M1105", 1, "A000600-0", "100", "As01"), new("M1105", 2, "A000600-0", "100", "As02")],
        [
            new("M1105", 1, "As", "Aslan Hierate"),
            new("M1105", 1, "As01", "Clan One"),
            new("M1105", 2, "As02", "Clan Two"),
        ]
    )
    .Single();
Check(
    namedMajorPolity.TopPolities.Single()
        is { Code: "As", Name: "Aslan Hierate", Population: 2_000_000m },
    "Major polity names prefer two-letter metadata rather than a constituent allegiance"
);
var fallbackPolities = SectorPopulation
    .Calculate(
        ["M1105"],
        [
            new("M1105", 1, "A000100-0", "100", "NaHu"),
            new("M1105", 2, "A000100-0", "100", "naAs"),
            new("M1105", 1, "A000100-0", "100", "JuPr"),
        ],
        [new("M1105", 1, "NaHu", "Independent Humans")]
    )
    .Single();
Check(
    fallbackPolities.TopPolities.Single(p => p.Code == "Na")
        is { Name: "Non-aligned or Independent", Population: 20 }
        && fallbackPolities.TopPolities.Single(p => p.Code == "Ju").Name == "Vassalry Judiciate",
    "Na and Ju use the supplied major polity fallback names"
);
var explicitPolityName = SectorPopulation
    .Calculate(
        ["M1105"],
        [new("M1105", 1, "A000100-0", "100", "NaHu")],
        [new("M1105", 1, "Na", "Custom Independent Polity")]
    )
    .Single();
Check(
    explicitPolityName.TopPolities.Single().Name == "Custom Independent Polity",
    "Explicit major polity metadata retains precedence over fallback names"
);
var oneOtherPolity = SectorPopulation
    .Calculate(["M1105"], populationFixtureWorlds.Take(11), populationFixtureAllegiances)
    .Single();
Check(
    oneOtherPolity.OtherPolityCount == 1 && oneOtherPolity.Others.Population == 1_000_000m,
    "Others count reflects the number of groups outside the top ten"
);
Check(
    SectorPopulation.Calculate(["M1105"], [], []).Single().OtherPolityCount == 0,
    "Empty population statistics have zero other polities"
);
var unspecifiedPopulation = SectorPopulation
    .Calculate(["M1105"], [new("M1105", 1, "A000100-0", "100", "????")], [])
    .Single();
Check(
    unspecifiedPopulation.TotalPopulation == 10
        && unspecifiedPopulation.TopPolities.Single().Name == "Unspecified polity",
    "Missing allegiance does not discard known population"
);
var hugePopulation = SectorPopulation
    .Calculate(
        ["M1105"],
        Enumerable
            .Range(0, 2000)
            .Select(_ => new PopulationWorld("M1105", 1, "A000F00-0", "900", "MEGA")),
        []
    )
    .Single();
Check(
    hugePopulation.TotalPopulation == 18_000_000_000_000_000_000m,
    "Population aggregation avoids 64-bit integer overflow"
);
Check(
    PassengerTraffic.DiceFor(1) == 0
        && PassengerTraffic.DiceFor(2) == 1
        && PassengerTraffic.DiceFor(6) == 2
        && PassengerTraffic.DiceFor(10) == 3
        && PassengerTraffic.DiceFor(13) == 4
        && PassengerTraffic.DiceFor(15) == 5
        && PassengerTraffic.DiceFor(16) == 6
        && PassengerTraffic.DiceFor(19) == 9
        && PassengerTraffic.DiceFor(20) == 10,
    "Passenger traffic table boundaries"
);
var passengers = PassengerTraffic.Roll("A788899-C", "A", 1, 2, () => 1);
Check(
    passengers.PopulationDm == 3 && passengers.StarportDm == 2 && passengers.ZoneDm == 1,
    "Passenger world modifiers"
);
Check(
    passengers.Lines.Count == 4
        && passengers.Lines[0]
            is { Passage: "High", Roll: 2, ModifiedRoll: 7, Dice: 3, Passengers: 3 }
        && passengers.Lines[3] is { Passage: "Low", ModifiedRoll: 12, Dice: 4, Passengers: 4 },
    "Passenger class modifiers and independent rolls"
);
var sparsePassengers = PassengerTraffic.Roll("X000100-0", "R", 0, 0, () => 1);
Check(
    sparsePassengers.PopulationDm == -4
        && sparsePassengers.StarportDm == -3
        && sparsePassengers.ZoneDm == -4
        && sparsePassengers.Lines.All(line => line.Passengers == 0),
    "Low population red zone passenger modifiers"
);
var routePassengers = PassengerTraffic.RollRoute(
    "A788899-C",
    "A",
    "X000100-0",
    "R",
    3,
    42,
    "0101",
    1,
    2,
    () => 1
);
Check(
    routePassengers.Destination is { PopulationDm: -4, StarportDm: -3, ZoneDm: -4 }
        && routePassengers.Distance == 3
        && routePassengers.Lines[0].ModifiedRoll == -6,
    "Passenger route applies both worlds and distance"
);
var nearPassengers = PassengerTraffic.RollRoute(
    "A788899-C",
    "A",
    "A788899-C",
    "A",
    1,
    42,
    "0101",
    1,
    2,
    () => 1
);
var farPassengers = PassengerTraffic.RollRoute(
    "A788899-C",
    "A",
    "A788899-C",
    "A",
    6,
    42,
    "0101",
    1,
    2,
    () => 1
);
Check(
    nearPassengers.Lines[0].ModifiedRoll - farPassengers.Lines[0].ModifiedRoll == 5,
    "Distance DM starts after the first parsec"
);
var withoutSteward = PassengerTraffic.RollRoute(
    "A788899-C",
    "A",
    "A788899-C",
    "A",
    1,
    42,
    "0101",
    0,
    0,
    () => 1,
    hasSteward: false
);
Check(
    !withoutSteward.HasSteward
        && withoutSteward.Lines[0] is { Passage: "High", Available: false, Passengers: 0 }
        && withoutSteward.Lines.Skip(1).All(l => l.Available),
    "High passage unavailable without a Steward"
);
var unskilledSteward = PassengerTraffic.RollRoute(
    "A788899-C",
    "A",
    "A788899-C",
    "A",
    1,
    42,
    "0101",
    0,
    -2,
    () => 1,
    hasSteward: false
);
Check(
    unskilledSteward.Lines[1].ModifiedRoll == withoutSteward.Lines[1].ModifiedRoll - 2
        && !unskilledSteward.Lines[0].Available,
    "Unskilled Steward DM applies to available passage classes"
);
Check(
    System
        .Text.Json.JsonSerializer.Deserialize<PassengerResult>(
            System.Text.Json.JsonSerializer.Serialize(withoutSteward)
        )
        ?.Lines[0]
        .Available == false,
    "Unavailable High passage survives serialization"
);
Check(
    System
        .Text.Json.JsonSerializer.Deserialize<PassengerLine>(
            "{\"Passage\":\"High\",\"Roll\":2,\"PassageDm\":-4,\"ModifiedRoll\":4,\"Dice\":1,\"Passengers\":1}"
        )
        ?.Available == true,
    "Earlier saved passenger lines remain available"
);
var negativeSteward = PassengerTraffic.RollRoute(
    "A788899-C",
    "A",
    "A788899-C",
    "A",
    1,
    42,
    "0101",
    0,
    -2,
    () => 1
);
Check(
    negativeSteward.Lines[0].Available && negativeSteward.Steward == -2,
    "A Steward with a negative combined DM still enables High passage"
);
try
{
    PassengerTraffic.RollRoute("A788899-C", "A", "A788899-C", "A", 0, 42, "0101", 0, 0);
    throw new Exception("Invalid distance accepted");
}
catch (ArgumentException) { }
try
{
    PassengerTraffic.Roll("???????-?", "", 0, 0);
    throw new Exception("Unknown UWP accepted");
}
catch (ArgumentException) { }

var selectedPassengers = PassengerTraffic.Select(passengers, "High", 2);
selectedPassengers = PassengerTraffic.Select(selectedPassengers, "Low", 3);
Check(
    PassengerTraffic
        .Fees.Select(f => (f.High, f.Middle, f.Basic, f.Low))
        .SequenceEqual(
            new[]
            {
                (9000, 6500, 2000, 700),
                (14000, 10000, 3000, 1300),
                (21000, 14000, 5000, 2200),
                (34000, 23000, 8000, 3900),
                (60000, 40000, 14000, 7200),
                (210000, 130000, 55000, 27000),
            }
        ),
    "Updated passage fees for all distances"
);
Check(
    PassengerTraffic.Fees[0].Payment(selectedPassengers) == 20100
        && PassengerTraffic.Fees[5].Payment(selectedPassengers) == 501000,
    "Passenger payments use selected classes and distance"
);
try
{
    PassengerTraffic.Select(passengers, "High", 4);
    throw new Exception("Too many passengers accepted");
}
catch (ArgumentException) { }
try
{
    PassengerTraffic.Select(passengers, "Low", -1);
    throw new Exception("Negative passenger selection accepted");
}
catch (ArgumentException) { }
Check(
    FreightTraffic.DiceFor(1) == 0
        && FreightTraffic.DiceFor(3) == 1
        && FreightTraffic.DiceFor(5) == 2
        && FreightTraffic.DiceFor(8) == 3
        && FreightTraffic.DiceFor(11) == 4
        && FreightTraffic.DiceFor(14) == 5
        && FreightTraffic.DiceFor(16) == 6
        && FreightTraffic.DiceFor(19) == 9
        && FreightTraffic.DiceFor(20) == 10,
    "Freight traffic table boundaries"
);
var freight = FreightTraffic.Roll("A788899-C", "A", 1, () => 1);
Check(
    freight.PopulationDm == 4
        && freight.StarportDm == 2
        && freight.TechDm == 2
        && freight.ZoneDm == -2,
    "Freight world modifiers"
);
Check(
    freight.Lines[0] is { Type: "Major", ModifiedRoll: 5, Dice: 2, Lots: 2 }
        && freight.Lines[1] is { Type: "Minor", ModifiedRoll: 9, Dice: 4, Lots: 4 }
        && freight.Lines[2] is { Type: "Incidental", ModifiedRoll: 11, Dice: 4, Lots: 4 },
    "Freight type modifiers and independent rolls"
);
var routeFreight = FreightTraffic.RollRoute(
    "A788899-C",
    "A",
    "X000100-0",
    "R",
    3,
    42,
    "0101",
    1,
    () => 1
);
Check(
    routeFreight.Destination is { PopulationDm: -4, StarportDm: -3, TechDm: -1, ZoneDm: -6 }
        && routeFreight.Distance == 3
        && routeFreight.Lines[0].ModifiedRoll == -11,
    "Freight route applies both worlds and distance"
);
var nearFreight = FreightTraffic.RollRoute(
    "A788899-C",
    "A",
    "A788899-C",
    "A",
    1,
    42,
    "0101",
    1,
    () => 1
);
var farFreight = FreightTraffic.RollRoute(
    "A788899-C",
    "A",
    "A788899-C",
    "A",
    6,
    42,
    "0101",
    1,
    () => 1
);
Check(
    nearFreight.Lines[0].ModifiedRoll - farFreight.Lines[0].ModifiedRoll == 5,
    "Freight distance DM starts after the first parsec"
);
Check(
    FreightTraffic.RollMail(routeFreight, 0, 0, () => 1, armed: false).Mail
        is { FreightTrafficDm: -9, Effect: -1 },
    "Mail traffic uses the full freight route DM"
);
try
{
    FreightTraffic.RollRoute("A788899-C", "A", "A788899-C", "A", 7, 42, "0101", 0);
    throw new Exception("Invalid freight distance accepted");
}
catch (ArgumentException) { }
var loadingInput = freight with
{
    Lines = freight
        .Lines.Select(line =>
            line with
            {
                Lots = line.Type switch
                {
                    "Major" => 3,
                    "Minor" => 1,
                    _ => 1,
                },
            }
        )
        .Reverse()
        .ToArray(),
};
var cargoDice = new Queue<int>([6, 2, 1, 2, 6]);
var loadedCargo = FreightTraffic.TakeCargo(loadingInput, 16, () => cargoDice.Dequeue());
Check(
    loadedCargo.CargoCapacity == 16
        && loadedCargo.Lines.Single(l => l.Type == "Major").CargoTaken == 10
        && loadedCargo.Lines.Single(l => l.Type == "Minor").CargoTaken == 0
        && loadedCargo.Lines.Single(l => l.Type == "Incidental").CargoTaken == 6
        && cargoDice.Count == 0,
    "Cargo loading prioritizes classes, skips oversized lots and fills remaining capacity"
);
var noCargo = FreightTraffic.TakeCargo(
    loadingInput,
    0,
    () => throw new Exception("No dice at zero capacity")
);
Check(noCargo.Lines.All(l => l.CargoTaken == 0), "Zero cargo capacity");
try
{
    FreightTraffic.TakeCargo(loadingInput, -1);
    throw new Exception("Negative capacity accepted");
}
catch (ArgumentException) { }
Check(
    FreightTraffic.MailTrafficDm(-10) == -2
        && FreightTraffic.MailTrafficDm(-9) == -1
        && FreightTraffic.MailTrafficDm(-4) == 0
        && FreightTraffic.MailTrafficDm(4) == 0
        && FreightTraffic.MailTrafficDm(5) == 1
        && FreightTraffic.MailTrafficDm(10) == 2,
    "Mail traffic modifier bands"
);
var mailDice = new Queue<int>([3, 3, 3]);
var withMail = FreightTraffic.RollMail(freight, 2, 1, () => mailDice.Dequeue());
Check(
    withMail.Mail
        is {
            FreightTrafficDm: 7,
            Effect: 6,
            TechDm: 0,
            Roll: 6,
            ModifiedRoll: 12,
            Containers: 3,
            ArmedDm: 2
        },
    "Mail succeeds at 12 and rolls containers"
);
var unarmedMail = FreightTraffic.RollMail(freight, 2, 1, () => 3, armed: false);
Check(
    unarmedMail.Mail is { Effect: 4, ArmedDm: 0, Roll: 6, ModifiedRoll: 10, Containers: 0 },
    "Unarmed ship omits mail DM"
);
var lowTechMail = FreightTraffic.RollMail(freight with { Tech = "5" }, 0, 0, () => 1);
Check(lowTechMail.Mail is { TechDm: -4, Containers: 0 }, "Mail failure and low tech penalty");
var cannotFit = FreightTraffic.TakeMail(withMail, 14);
Check(cannotFit.Mail is { CargoTaken: 0, InsufficientSpace: true }, "Mail cannot exceed capacity");
var acceptedMail = FreightTraffic.TakeMail(withMail, 20);
Check(
    acceptedMail.Mail is { CargoTaken: 15, InsufficientSpace: false },
    "Mail accepted when it fits"
);
var afterMailCargo = FreightTraffic.TakeCargo(acceptedMail, 20, () => 1);
Check(
    afterMailCargo.Lines.Sum(l => l.CargoTaken ?? 0) <= 5 && afterMailCargo.Mail?.CargoTaken == 15,
    "Freight respects accepted mail capacity"
);
try
{
    FreightTraffic.TakeCargo(acceptedMail, 14);
    throw new Exception("Cargo capacity below accepted mail allowed");
}
catch (ArgumentException) { }
var sparseFreight = FreightTraffic.Roll("X000100-0", "R", 0, () => 1);
Check(
    sparseFreight.PopulationDm == -4
        && sparseFreight.StarportDm == -3
        && sparseFreight.TechDm == -1
        && sparseFreight.ZoneDm == -6
        && sparseFreight.Lines.All(line => line.Lots == 0),
    "Sparse red zone freight modifiers"
);
try
{
    FreightTraffic.Roll("A788899-?", "", 0);
    throw new Exception("Unknown freight tech level accepted");
}
catch (ArgumentException) { }

var dateCampaign = new Campaign(1, "Date check", 2, 1105, null, null, true);
var today = CampaignDates.Today(dateCampaign);
Check(
    CampaignDates.Status(today, dateCampaign).Text == "Current"
        && CampaignDates.Status(today + 1, dateCampaign).Text == "Current",
    "Today and future are current"
);
Check(
    CampaignDates.Status(today - 1, dateCampaign).Text == "Outdated 1 days"
        && CampaignDates.Status(today - 6, dateCampaign).Style == "outdated",
    "Recent results outdated"
);
Check(
    CampaignDates.Status(today - 7, dateCampaign) == ("Expired 7 days", "expired"),
    "Seven days expires across year boundary"
);
Check(
    CampaignDates.Status(null, dateCampaign).Text == "Date unknown"
        && CampaignDates.Status(today, null).Text == "Select campaign",
    "Undated legacy and missing campaign status"
);
Check(
    CampaignDates.RemoteTarget(today, 3, () => 1) == today - 7
        && CampaignDates.RemoteTarget(today, 4, () => 6) == today - 19,
    "Remote destination delay and distance boundary"
);
Check(
    CampaignDates.Status(today - 7, dateCampaign with { Day = 1, Year = 1105 }).Text
        == "Outdated 6 days",
    "Date status updates when campaign date changes"
);

Check(
    CharacterSkillCatalogue.All.Count == 39
        && CharacterSkillCatalogue
            .All.Select(s => s.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == 39,
    "Core skill catalogue coverage"
);
Check(
    CharacterSkillCatalogue.Find("pilot")!.Specialities.Contains("Spacecraft")
        && CharacterSkillCatalogue.Find("Electronics")!.Specialities.Contains("Computers"),
    "Core skill specialities"
);
Check(
    CharacterSkillCatalogue.Parse("pilot (Spacecraft)")
        is { Skill.Name: "Pilot", Speciality: "Spacecraft" },
    "Saved specialised skill parses"
);
Check(
    CharacterSkillCatalogue.Parse("Science (Custom/Field)")
        is { Skill.Name: "Science", Speciality: "Custom/Field" },
    "Custom specialty parses"
);
Check(
    CharacterSkillCatalogue.Parse("Custom skill") is { Skill: null }
        && CharacterSkillCatalogue.Parse("Broker") is { Skill.Name: "Broker", Speciality: null },
    "Custom and unspecialised skills remain editable"
);

Check(
    CharacterAge.Calculate("Navy 3, Diplomat 1", 1105) == 32
        && CharacterAge.Calculate("3", 1106) == 29,
    "Age combines terms and campaign year"
);
Check(
    CharacterAge.Calculate("Navy", 1105) is null
        && CharacterAge.Calculate("", 1105) is null
        && CharacterAge.Calculate("Navy -3", 1105) is null
        && CharacterAge.Calculate("Navy 3,", 1105) is null,
    "Unparseable terms do not invent an age"
);

Check(PoliticalColor.Resolve(null, "ImDd", false) == "red", "Imperial border fallback");
Check(PoliticalColor.Resolve("", "Im", true) == "limegreen", "Imperial route fallback");
Check(PoliticalColor.Resolve(null, "ZhIN", false) == "blue", "Zhodani border fallback");
Check(PoliticalColor.Resolve(null, "ZhCo", true) == "lightblue", "Zhodani route fallback");
Check(
    PoliticalColor.Resolve("orange", "ImDd", false) == "orange",
    "Stored colours take precedence"
);
Check(
    PoliticalColor.Resolve(null, "DaCf", true) is null
        && PoliticalColor.Resolve(null, null, true) is null,
    "Other allegiances retain existing fallback"
);
Check(PoliticalColor.Resolve(null, "AsMw", false) == "yellow", "Aslan border fallback");
Check(
    PoliticalColor.Resolve("purple", "AsMw", false) == "purple",
    "Aslan stored border colour takes precedence"
);
Check(PoliticalColor.Resolve(null, "AsMw", true) is null, "Aslan route colour unchanged");
var profile = UwpLookup.Describe("a788899-c");
Check(
    profile.Count == 8 && profile[0].Code == 'A' && profile[7].Code == 'C',
    "UWP positions or case handling"
);
Check(profile.All(a => !a.Description.Contains("Unrecognised")), "Known profile descriptions");
foreach (var value in new string?[] { null, "", "A78", "A788899?C", "A788899-CC", "?????????" })
    Check(
        UwpLookup.Describe(value).All(a => a.Description == "Unknown"),
        "Malformed profile handling"
    );
Check(UwpLookup.Describe("A78?899-C")[3].Description == "Unknown", "Unknown attribute handling");
Check(
    UwpLookup.Describe("!788899-C")[0].Description.Contains("Unrecognised"),
    "Unsupported code handling"
);
Check(UwpLookup.Describe("X000000-0")[1].Description == "Asteroid belt", "Asteroid size");
Check(UwpLookup.Describe("X000000-0")[4].Description == "Uninhabited", "Population zero");
var populationDescription = UwpLookup.Describe("A788899-C", "703")[4].Description;
Check(
    populationDescription.StartsWith(profile[4].Description)
        && populationDescription.Contains($"Population: {700_000_000:N0}."),
    "Population multiplier and existing description"
);
Check(
    UwpLookup.Describe("X000000-0", "000")[4].Description == "Uninhabited. Population: 0.",
    "Uninhabited population"
);
Check(
    UwpLookup.Describe("X000000-0", "503")[4].Description.Contains("Population: 5."),
    "Exponent zero with nonzero multiplier"
);
Check(
    UwpLookup
        .Describe("A788F99-C", "903")[4]
        .Description.Contains($"Population: {9_000_000_000_000_000L:N0}."),
    "Large population"
);
foreach (var pbg in new string?[] { null, "", "?03", "A03" })
    Check(
        UwpLookup.Describe("A788899-C", pbg)[4].Description == profile[4].Description,
        "Unavailable multiplier"
    );
Check(
    UwpLookup.Describe("A788?99-C", "703")[4].Description == "Unknown",
    "Unknown population exponent"
);

var landDescription = UwpLookup.Describe("A868899-C")[3].Description;
Check(
    UwpLookup.Describe("A868899-C")[1].Description
        == $"Approximately {12_800:N0} km diameter. Surface gravity: 1 g",
    "Earth-sized world gravity and retained diameter"
);
Check(
    UwpLookup.Describe("A168899-C")[1].Description.Contains($"{0.122:0.###} g"),
    "Small world gravity"
);
Check(
    UwpLookup.Describe("AF68899-C")[1].Description.Contains($"{1.875:0.###} g"),
    "Largest standard size gravity"
);
foreach (var uwp in new[] { "A068899-C", "AS68899-C", "A?68899-C", "AG68899-C", "invalid" })
    Check(
        !UwpLookup
            .Describe(uwp)[1]
            .Description.Contains("surface gravity", StringComparison.OrdinalIgnoreCase),
        "No gravity estimate without a standard size"
    );
Check(
    landDescription.StartsWith("Approximately 80% surface liquid")
        && landDescription.Contains($"{77_207_781:N0}–{128_679_635:N0} km²."),
    "Spherical surface and land area range"
);
Check(
    UwpLookup
        .Describe("A860899-C")[3]
        .Description.Contains($"{488_982_613:N0}–{514_718_540:N0} km²."),
    "Dry world lower clamp"
);
Check(
    UwpLookup.Describe("A86A899-C")[3].Description.Contains($"{0:N0}–{25_735_927:N0} km²."),
    "Water world upper clamp"
);
foreach (
    var uwp in new[]
    {
        "A068899-C",
        "AS68899-C",
        "A?68899-C",
        "AG68899-C",
        "A86?899-C",
        "A86B899-C",
        "invalid",
    }
)
    Check(
        !UwpLookup.Describe(uwp)[3].Description.Contains("land area"),
        "Unavailable size or hydrographics"
    );

var tradeCodes = TradeCodeLookup.Describe(
    "Ht Ri Pa Ph An Cp (Amindii)2 O:0101 { Hi notes } [Human]"
);
Check(
    tradeCodes
        .Select(code => code.Code)
        .SequenceEqual(new[] { "Ht", "Ri", "Pa", "Ph", "An", "Cp" }),
    "Trade codes from stored remarks, excluding other annotations"
);
Check(
    tradeCodes[1].Name == "Rich" && tradeCodes.All(code => code.Description.Length > 0),
    "Central trade descriptions"
);
Check(TradeCodeLookup.Describe("Ag ag AG").Count == 1, "Duplicate trade codes");
Check(TradeCodeLookup.Describe("RsA")[0].Code == "RsA", "Designated research station");
Check(TradeCodeLookup.Describe("Zz")[0].Name == "Unrecognised trade code", "Custom code fallback");
Check(
    TradeCodeLookup.Describe(null).Count == 0
        && TradeCodeLookup.Describe("- (Minor race) [Human]").Count == 0,
    "Empty or non-trade remarks"
);

SelectedWorld SystemWorld(
    string uwp = "A788899-C",
    string pbg = "713",
    string w = "8",
    string stellar = "F7 V BD M3 V",
    string remarks = ""
) => new("Test", "Test sector", "0101", uwp, pbg, w, stellar, remarks, "", null);

var systemData = SystemDataLookup.Describe(SystemWorld());
Check(
    systemData.WorldDescription == "9 worlds, including 3 gas giants and 1 asteroid belt.",
    "Combined world description"
);
Check(
    SystemDataLookup.Describe(SystemWorld(pbg: "703")).WorldDescription
        == "9 worlds, including 3 gas giants.",
    "Omit absent asteroid belts"
);
Check(
    SystemDataLookup.Describe(SystemWorld(pbg: "700", w: "0")).WorldDescription == "1 world.",
    "Singular world and no empty inclusions"
);
Check(
    SystemDataLookup.Describe(SystemWorld(pbg: "701", remarks: "Sa")).WorldDescription
        == "8 worlds, including 1 gas giant. The main world is a satellite.",
    "Satellite description"
);
Check(
    SystemDataLookup.Describe(SystemWorld(uwp: "X000000-0", pbg: "700")).WorldDescription
        == "8 worlds, including 1 asteroid belt. The main world is an asteroid belt.",
    "Main-world belt description"
);
Check(
    SystemDataLookup.Describe(SystemWorld(pbg: "???", w: "?")).WorldDescription
        == "Unknown number of worlds.",
    "Unknown counts remain unknown"
);
Check(
    systemData.Stars.Select(s => s.Designation).SequenceEqual(new[] { "A", "B", "C" }),
    "Stellar letter designations in source order"
);
Check(
    systemData is { Worlds: 9, GasGiants: 3, AsteroidBelts: 1, StellarDataKnown: true }
        && systemData.Stars.Count == 3,
    "System totals and multi-star classification"
);
Check(
    systemData.Stars[0] is { Code: "F7 V", Description: "Yellow-white main-sequence star" }
        && systemData.Stars[1].Description == "Brown dwarf"
        && systemData.Stars[2].Description == "Red main-sequence star",
    "Star types"
);
Check(
    SystemDataLookup.Describe(SystemWorld(uwp: "X000000-0")) is { Worlds: 8, AsteroidBelts: 2 },
    "Main-world asteroid belt adjustments"
);
Check(
    SystemDataLookup.Describe(SystemWorld(remarks: "Hi Sa Ht")).Worlds == 8,
    "Main-world moon excluded from extra worlds"
);
Check(
    SystemDataLookup.Describe(SystemWorld(remarks: "(Saurian) O:0101")).Worlds == 9,
    "Moon code must be a complete remark"
);
Check(
    SystemDataLookup.Describe(SystemWorld(pbg: "7AB", w: "12"))
        is { Worlds: 13, GasGiants: 11, AsteroidBelts: 10 },
    "Decimal W and extended-hex PBG counts"
);
Check(
    SystemDataLookup.Describe(SystemWorld(pbg: "700", w: "0"))
        is { Worlds: 1, GasGiants: 0, AsteroidBelts: 0 },
    "Known zero counts"
);
Check(
    SystemDataLookup.Describe(SystemWorld(pbg: "7?3", w: "?"))
        is { Worlds: null, GasGiants: 3, AsteroidBelts: null },
    "Independent unknown counts"
);
Check(
    SystemDataLookup.Describe(SystemWorld(uwp: "A?88899-C"))
        is { Worlds: null, AsteroidBelts: null },
    "Unknown main-world classification"
);
foreach (var value in new[] { "", "-", "?", "F7 V unknown", "F7 V ?", "garbage" })
    Check(
        !SystemDataLookup.Describe(SystemWorld(stellar: value)).StellarDataKnown,
        "Incomplete stellar data must not imply a known count"
    );
var compactStars = SystemDataLookup.Describe(
    SystemWorld(stellar: "G2V M1 VI K3 III D DA BH NS PSR")
);
Check(
    compactStars.StellarDataKnown
        && compactStars.Stars.Count == 8
        && compactStars.Stars[1].Description == "Red subdwarf"
        && compactStars.Stars[2].Description == "Orange giant"
        && compactStars.Stars[3].Description == "White dwarf"
        && compactStars.Stars[5].Description == "Black hole",
    "Stellar luminosity classes and remnants"
);

using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var services = new ServiceCollection();
var commands = new CatalogueCommandCounter();
services.AddDbContext<MtuDbContext>(options =>
    options.UseSqlite(connection).AddInterceptors(commands)
);
services.AddMtuCatalogue();
using var provider = services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }
);
using (var scope = provider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
    await db.Database.EnsureCreatedAsync();
    db.Sectors.AddRange(
        new SectorEntity
        {
            Name = "Spinward Marches",
            X = -4,
            Y = -1,
            Milieu = "M1105",
            Subsectors = [new SubsectorEntity { Letter = "C", Name = "Regina" }],
            Allegiances =
            [
                new AllegianceEntity { Code = "ImDd", Name = "Third Imperium, Domain of Deneb" },
            ],
            Systems =
            [
                new SystemEntity
                {
                    Name = "Regina",
                    Hex = "1910",
                    Uwp = "A788899-C",
                    Pbg = "703",
                    W = "8",
                    Stellar = "F7 V BD M3 V",
                    Remarks = "Sa",
                    A = "ImDd",
                },
                new SystemEntity
                {
                    Name = "Unmapped polity",
                    Hex = "1911",
                    A = "Custom",
                },
                new SystemEntity { Name = "No allegiance", Hex = "1912" },
            ],
        },
        new SectorEntity
        {
            Name = "Different era",
            X = -4,
            Y = -1,
            Milieu = "M1120",
            Allegiances = [new AllegianceEntity { Code = "ImDd", Name = "Different era polity" }],
            Systems =
            [
                new SystemEntity
                {
                    Name = "Other Regina",
                    Hex = "1910",
                    Uwp = "X000000-0",
                    A = "ImDd",
                },
            ],
        }
    );
    await db.SaveChangesAsync();
}
commands.Reads = 0;
var catalogueProvider = provider.GetRequiredService<SystemDataProvider>();
await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => catalogueProvider.InitializeAsync()));
Check(commands.Reads == 6, "Concurrent initialization reads each catalogue table exactly once");
var snapshot = catalogueProvider.Catalogue;

await catalogueProvider.InitializeAsync();
Check(
    ReferenceEquals(snapshot, catalogueProvider.Catalogue) && commands.Reads == 6,
    "Initialization reuses the published snapshot"
);
Check(
    snapshot.Systems.Length == 4 && snapshot.Systems.All(s => s.Bodies.IsEmpty),
    "All milieus loaded with no invented system bodies"
);
var spinward = snapshot.Sectors.Single(s => s.Milieu == "M1105");
var adjacent = new CatalogueSector(-1, "Adjacent", spinward.X + 1, spinward.Y, "M1105");
Check(
    TradeDestinations.Distance(spinward, "3201", adjacent, "0101") == 1,
    "Distance across sector seam"
);
Check(
    TradeDestinations.Distance(adjacent, "0101", spinward, "3201") == 1,
    "Distance symmetric across negative coordinates"
);
Check(
    TradeDestinations.Distance(spinward, "1910", spinward, "1916") == 6
        && TradeDestinations.Distance(spinward, "1910", spinward, "1917") == 7,
    "Six parsec distance boundary"
);
Check(
    TradeDestinations.Distance(spinward, "0000", spinward, "1910") is null,
    "Invalid hex does not produce a destination"
);
var nearby = TradeDestinations.Within(snapshot, spinward.Id, "1910");
Check(
    nearby
        .Select(d => d.System.MainWorld.Name)
        .SequenceEqual(new[] { "No allegiance", "Unmapped polity" }),
    "Nearby worlds sorted alphabetically with current world and other milieu excluded"
);
Check(nearby.All(d => d.Distance <= 6), "Only nearby destinations included");
commands.Reads = 0;

// Destroy the in-memory database: every consumer must now work solely from the snapshot.
await connection.CloseAsync();
using (var scope = provider.CreateScope())
{
    Check(
        ReferenceEquals(
            catalogueProvider,
            scope.ServiceProvider.GetRequiredService<SystemDataProvider>()
        ),
        "Catalogue shared across scopes"
    );
    var selection = scope.ServiceProvider.GetRequiredService<WorldSelectionService>();
    var world = await selection.FindAsync(-4, -1, 19, 10);
    Check(
        world
            is {
                Name: "Regina",
                Sector: "Spinward Marches",
                Hex: "1910",
                Uwp: "A788899-C",
                Pbg: "703",
                W: "8",
                Stellar: "F7 V BD M3 V",
                Remarks: "Sa"
            },
        "Database selection, system data and milieu"
    );
    Check(
        world is { AllegianceCode: "ImDd", AllegianceName: "Third Imperium, Domain of Deneb" },
        "Sector-specific allegiance polity lookup"
    );
    Check(
        await selection.FindAsync(-4, -1, 19, 11)
            is { AllegianceCode: "Custom", AllegianceName: null },
        "Unmapped allegiance retains the world and code"
    );
    Check(
        await selection.FindAsync(-4, -1, 19, 12) is { AllegianceCode: "", AllegianceName: null },
        "Missing allegiance retains the world"
    );
    Check(await selection.FindAsync(-4, -1, 1, 1) is null, "Empty hex");
    Check(await selection.FindAsync(-3, -1, 19, 10) is null, "Different sector");
    Check(await selection.FindAsync(-4, -1, 0, 10) is null, "Invalid hex");
    await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => selection.FindAsync(-4, -1, 19, 10)));
}
Console.WriteLine("UWP parsing and SQLite world selection checks passed.");

using (var scope = provider.CreateScope())
{
    var service = scope.ServiceProvider.GetRequiredService<MapDataService>();
    var overview = await service.GetOverviewAsync();
    Check(
        overview.Sectors.Count == 1 && overview.Sectors[0].Name == "Spinward Marches",
        "Database map overview filters milieu"
    );
    Check(
        overview.Sectors[0].Hexes.Count == 3 && overview.MinSectorX == TileRefreshWorker.MinX,
        "Overview points and atlas origin without tile files"
    );
    Check(
        overview.Sectors[0].Worlds.Single(w => w.Hex == "1910")
            is { Name: "Regina", Uwp: "A788899-C", Remarks: "Sa" },
        "Overview includes world label classifications before sector details load"
    );
    var details = await service.GetSectorAsync(overview.Sectors[0].Id);
    Check(
        details is not null
            && details.Single(w => w.Hex == "1910")
                is {
                    Name: "Regina",
                    Uwp: "A788899-C",
                    Pbg: "703",
                    Remarks: "Sa",
                    Allegiance: "ImDd"
                },
        "Sector detail fields"
    );
    Check(await service.GetSectorAsync(-1) is null, "Missing sector is not an empty valid sector");
    var otherEra = snapshot.Sectors.Single(s => s.Milieu == "M1120");
    Check(await service.GetSectorAsync(otherEra.Id) is null, "Sector detail filters milieu");
}
using (var emptyConnection = new SqliteConnection("Data Source=:memory:"))
{
    await emptyConnection.OpenAsync();
    using var emptyDb = new MtuDbContext(
        new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(emptyConnection).Options
    );
    await emptyDb.Database.EnsureCreatedAsync();
    var emptyServices = new ServiceCollection();
    emptyServices.AddDbContext<MtuDbContext>(options => options.UseSqlite(emptyConnection));
    emptyServices.AddMtuCatalogue();
    emptyServices.AddScoped<SectorDataService>();
    using var emptyProvider = emptyServices.BuildServiceProvider();
    await emptyProvider.GetRequiredService<SystemDataProvider>().InitializeAsync();
    var emptySectorStats = await emptyProvider
        .GetRequiredService<SectorDataService>()
        .GetStatisticsAsync();
    Check(
        emptySectorStats
            is {
                Sectors: 0,
                SectorNames: 0,
                Subsectors: 0,
                Systems: 0,
                Allegiances: 0,
                Borders: 0,
                Routes: 0,
                LastRefreshUtc: null
            }
            && emptySectorStats.Milieus.Count == 0,
        "Sector data statistics handle an empty database without inventing a refresh date"
    );
    var overview = await emptyProvider.GetRequiredService<MapDataService>().GetOverviewAsync();
    Check(
        overview.Sectors.Count == 0 && overview.Columns > 0 && overview.Rows > 0,
        "Empty database has usable atlas bounds"
    );
}
Console.WriteLine("Database-driven map overview, sector details and empty database checks passed.");

var search = provider.GetRequiredService<ISearchService>();
var results = await search.SearchAsync("  REGina  ");
Check(
    results.Count == 3 && results[0].SubsectorName == "Regina",
    "Search trimming, case insensitivity, subsector and system results"
);
var regina = results.Single(r => r.SystemName == "Regina");
Check(
    regina
        is {
            SectorName: "Spinward Marches",
            SubsectorLetter: "C",
            SubsectorName: "Regina",
            SubsectorHex: "0310",
            SectorHex: "1910"
        },
    "Search subsector context preserved"
);
Check(
    results.Any(r => r.SystemName == "Other Regina"),
    "Search retains its existing all-milieu scope"
);
regina.SystemName = "Changed UI result";
Check(
    (await search.SearchAsync("Regina")).Any(r => r.SystemName == "Regina"),
    "UI results cannot mutate the catalogue"
);
Check((await search.SearchAsync("re")).Count == 0, "Minimum search term length");
Check(
    (await search.SearchAsync("Spinward")).Single().SectorName == "Spinward Marches",
    "Sector search"
);
await Task.WhenAll(
    Enumerable
        .Range(0, 20)
        .Select(async _ =>
        {
            Check((await search.SearchAsync("Regina")).Count == 3, "Concurrent search snapshot");
            Check(
                (await provider.GetRequiredService<MapDataService>().GetOverviewAsync())
                    .Sectors
                    .Count == 1,
                "Concurrent map snapshot"
            );
        })
);
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();
    try
    {
        await search.SearchAsync("Regina", cancellation.Token);
        throw new Exception("Search ignored cancellation");
    }
    catch (OperationCanceledException) { }
    try
    {
        await provider.GetRequiredService<MapDataService>().GetOverviewAsync(cancellation.Token);
        throw new Exception("Map ignored cancellation");
    }
    catch (OperationCanceledException) { }
}
Check(
    commands.Reads == 0,
    "Search, map and selection perform no SQL after initialization, even with database unavailable"
);
Console.WriteLine(
    "Shared startup catalogue, concurrent initialization, search, snapshot isolation and zero request-time SQL checks passed."
);

// Purchase price uses 3D, one strongest matching modifier per column and the skill DM.
Check(
    TradePricing.Percentage(-20) == 200
        && TradePricing.Percentage(0) == 175
        && TradePricing.Percentage(8) == 100
        && TradePricing.Percentage(23) == 25
        && TradePricing.Percentage(40) == 25,
    "Purchase price table bounds"
);
var priceExample = TradePricing.Price(
    new MarketLine(11, "Example", 1, 10, false, false),
    2,
    "In Ht Ni Po",
    DiceForPrice(2, 3, 4)
);
Check(
    priceExample
        is {
            PriceRoll: 9,
            PurchaseDm: 3,
            SaleDm: 2,
            PricePercent: 80,
            BasePrice: 20000,
            BuyPrice: 16000
        },
    "Highest matching DMs and price per ton"
);
Check(
    TradePricing.FormatTradeModifiers(priceExample, false) == "+3/-2",
    "Buying displays individual trade DMs"
);
Check(
    TradePricing.FormatTradeModifiers(priceExample, true) == "+2/-3",
    "Selling displays individual trade DMs"
);
Check(
    TradePricing.FormatTradeModifiers(priceExample with { PurchaseDm = 0, SaleDm = 0 }, false)
        == "",
    "Zero trade DMs are blank"
);
Check(
    TradePricing.FormatTradeModifiers(priceExample with { PurchaseDm = 1, SaleDm = 1 }, false)
        == "+1/-1",
    "Offsetting trade DMs remain visible"
);
Check(
    TradePricing.FormatTradeModifiers(priceExample with { PurchaseDm = 0, SaleDm = -2 }, true)
        == "-2",
    "Negative sale DMs retain their sign"
);
Check(
    TradePricing.FormatTradeModifiers(priceExample with { PurchaseDm = null, SaleDm = null }, true)
        == "",
    "Missing trade DMs are blank"
);
var supplierExample = TradePricing.Price(
    new MarketLine(11, "Example", 1, 10, false, false),
    2,
    "In Ht Ni Po",
    DiceForPrice(2, 3, 4),
    -2
);
Check(
    supplierExample is { PricePercent: 90, BuyPrice: 18000 },
    "Signed supplier DM is added to the purchase roll"
);
var positiveSupplier = TradePricing.Price(
    new MarketLine(11, "Example", 1, 10, false, false),
    2,
    "In Ht Ni Po",
    DiceForPrice(2, 3, 4),
    2
);
Check(
    positiveSupplier is { PricePercent: 70, BuyPrice: 14000 },
    "Positive supplier DM is added rather than subtracted"
);
Check(
    TradePricing
        .Price(new MarketLine(66, "Exotics", 1, null, false, false), 0, "", DiceForPrice())
        .BuyPrice
        is null,
    "Exotics remain referee-priced"
);
static Func<int> DiceForPrice(params int[] values)
{
    var queue = new Queue<int>(values);
    return () => queue.Dequeue();
}

Check(
    TradePricing.SalePercentage(-99) == 30
        && TradePricing.SalePercentage(10) == 90
        && TradePricing.SalePercentage(11) == 100
        && TradePricing.SalePercentage(23) == 160
        && TradePricing.SalePercentage(99) == 160,
    "Sale price table boundaries"
);
var saleExample = TradePricing.SalePrice(
    new MarketLine(11, "Example", 0, null, false, false),
    2,
    "In Ht Ni Po",
    DiceForPrice(2, 3, 4)
);
Check(
    saleExample
        is {
            PriceRoll: 9,
            PurchaseDm: 3,
            SaleDm: 2,
            PricePercent: 90,
            BasePrice: 20000,
            SalePrice: 18000
        },
    "Sale price adds skill and highest sale DM, subtracts highest purchase DM"
);
var buyerExample = TradePricing.SalePrice(
    new MarketLine(11, "Example", 0, null, false, false),
    2,
    "In Ht Ni Po",
    DiceForPrice(2, 3, 4),
    -2
);
Check(
    buyerExample is { PricePercent: 80, SalePrice: 16000 },
    "Signed buyer DM is added to the sale roll"
);
var positiveBuyer = TradePricing.SalePrice(
    new MarketLine(11, "Example", 0, null, false, false),
    2,
    "In Ht Ni Po",
    DiceForPrice(2, 3, 4),
    2
);
Check(
    positiveBuyer is { PricePercent: 105, SalePrice: 21000 },
    "Positive buyer DM is added to the sale roll"
);

// Exact Imports membership for Regina's trade codes: commons plus matching positive Sale DMs.
var reginaImports = MarketGenerator.GenerateSales("Broker", 0, "Ri Pa Ph An Cp Sa Ht", () => 3);
int[] reginaExpected =
[
    11,
    12,
    13,
    14,
    15,
    16,
    21,
    23,
    25,
    31,
    32,
    34,
    35,
    36,
    42,
    43,
    44,
    45,
    46,
    51,
    54,
    55,
];
Check(
    reginaImports.Goods.Select(g => g.D66).SequenceEqual(reginaExpected),
    "Regina Imports match exact common and positive sale demand"
);
Check(
    !TradePricing.HasSaleDemand(26, "An") && TradePricing.HasSaleDemand(26, "In"),
    "Demand matches complete codes, not substrings"
);
Check(
    !TradePricing.HasSaleDemand(45, "Ag") && TradePricing.HasSaleDemand(45, "Ag Ht"),
    "Negative sale DM alone does not create demand"
);
Check(
    MarketGenerator.PopulationFromUwp("A788A99-C") == 10
        && MarketGenerator.PopulationFromUwp("A788099-C") == 0,
    "Population code including extended hex"
);
try
{
    MarketGenerator.PopulationFromUwp("A788?99-C");
    throw new Exception("Unknown population accepted");
}
catch (ArgumentException) { }
Check(
    MarketGenerator.PopulationTonnageDm(3) == -3
        && MarketGenerator.PopulationTonnageDm(4) == 0
        && MarketGenerator.PopulationTonnageDm(8) == 0
        && MarketGenerator.PopulationTonnageDm(9) == 3,
    "Population tonnage DM boundaries"
);
Check(
    MarketGenerator.DemandTons(TradeGoods.All.Single(g => g.D66 == 11), () => 1, -3) == 0
        && MarketGenerator.DemandTons(TradeGoods.All.Single(g => g.D66 == 11), () => 1, 3) == 50,
    "Population DMs apply before multiplier and clamp to zero"
);
var highDemand = MarketGenerator.GenerateSales("Broker", 0, "", () => 1, population: 9);
Check(
    highDemand.TonnageDm == 3 && highDemand.Goods.Single(g => g.D66 == 11).Tons == 50,
    "Destination population modifies selling demand"
);
var lowPopulation = MarketGenerator.Generate("Broker", 0, "", () => 1, population: 0);
var highPopulation = MarketGenerator.Generate("Broker", 0, "", () => 1, population: 10);
Check(
    lowPopulation.RandomRolls == 0
        && highPopulation.RandomRolls == 10
        && highPopulation.Goods.Single(g => g.D66 == 11).Lots == 11,
    "Population code determines random goods and duplicates add quantity"
);
Check(
    !TradePricing.HasSaleDemand(24, TradePricing.WithZone("Da Fo Fb", "")),
    "Remarks do not substitute for the stored travel zone"
);
Check(
    TradePricing.HasSaleDemand(24, TradePricing.WithZone("", "A"))
        && TradePricing.HasSaleDemand(24, TradePricing.WithZone("", "R")),
    "Travel zone enables sale demand"
);
var amberPrice = TradePricing.SalePrice(
    new MarketLine(24, "Weapons", 0, 0, false, false),
    0,
    TradePricing.WithZone("", "A"),
    () => 1
);
var redPrice = TradePricing.SalePrice(
    new MarketLine(24, "Weapons", 0, 0, false, false),
    0,
    TradePricing.WithZone("", "R"),
    () => 1
);
Check(amberPrice.SaleDm == 2 && redPrice.SaleDm == 4, "Stored zone modifies sale pricing");
Check(
    TradePricing
        .Price(
            new MarketLine(24, "Weapons", 0, 0, false, false),
            0,
            TradePricing.WithZone("Da", "R"),
            () => 1
        )
        .SaleDm == 4,
    "Red travel zone overrides conflicting remarks for buying price"
);
Check(
    TradePricing
        .Price(
            new MarketLine(24, "Weapons", 0, 0, false, false),
            0,
            TradePricing.WithZone("Fo RedZone", "A"),
            () => 1
        )
        .SaleDm == 2,
    "Amber travel zone overrides conflicting remarks"
);

var noCodes = MarketGenerator.GenerateSales("Broker", 0, "", () => 3);
Check(
    noCodes.Goods.Select(g => g.D66).SequenceEqual(new[] { 11, 12, 13, 14, 15, 16 }),
    "No trade codes produces only common goods"
);
var saleBroker = MarketGenerator.GenerateSales("Broker", 2, "In", () => 3);
var saleStreet = MarketGenerator.GenerateSales("Streetwise", 2, "In", () => 3);
Check(
    saleBroker.BuyerDm == -2
        && saleBroker.DemandVersion == 1
        && saleBroker.Goods.Single(g => g.D66 == 11).Tons == 60,
    "Demand uses commodity tonnage dice and records default buyer DM"
);
Check(
    saleBroker.Goods.Count(g => g.D66 <= 16) == 6
        && saleBroker.Goods.Any(g => g.D66 == 61) == false,
    "Broker sale demand includes common and excludes illegal goods"
);
Check(
    saleStreet.Goods.Any(g => g.D66 == 61 && g.Illegal) && saleStreet.Goods.All(g => g.D66 > 16),
    "Streetwise includes illegal matching demand but excludes common goods"
);
Check(!saleStreet.Goods.Any(g => g.D66 == 62), "Imports exclude nonmatching sale demand");

// Fixed dice streams cover random duplicates, illegal rerolls and stolen goods.
static Func<int> Dice(params int[] values)
{
    var queue = new Queue<int>(values);
    return () => queue.Count > 0 ? queue.Dequeue() : 1;
}
var brokerDice = Enumerable.Repeat(1, 12).Concat(new[] { 6, 1, 1, 1, 1, 1 }).ToArray();
var brokerMarket = MarketGenerator.Generate("Broker", 2, "", Dice(brokerDice), population: 1);
Check(
    brokerMarket.Goods.Count == 6 && brokerMarket.Goods.All(g => !g.Illegal && !g.Stolen),
    "Broker commons and illegal reroll"
);
Check(
    brokerMarket.Goods.Single(g => g.D66 == 11) is { Lots: 2, Tons: 0 },
    "Duplicate random good adds tonnage"
);
var streetMarket = MarketGenerator.Generate(
    "Streetwise",
    -1,
    "Ht",
    Dice(1, 1, 1, 1, 1, 1, 1),
    population: 1
);
Check(
    streetMarket.Goods.Count == 3
        && streetMarket.Goods.Count(g => g.Illegal) == 2
        && streetMarket.Goods.Single(g => g.D66 == 11).Stolen,
    "Streetwise illegal availability and stolen random goods"
);
Check(
    TradeGoods.All.Count == 36 && TradeGoods.All.Select(g => g.D66).Distinct().Count() == 36,
    "D66 commodity coverage"
);

var tradingSelections = new MTUAssistant.Services.TradingSelectionState();
var firstSelections = tradingSelections.ForCampaign(1);
firstSelections.BuyingTravellerId = 12;
firstSelections.BuyingMode = "Streetwise";
firstSelections.SellingTravellerId = 34;
Check(
    tradingSelections.ForCampaign(1)
        is {
            BuyingTravellerId: 12,
            BuyingMode: "Streetwise",
            SellingTravellerId: 34,
            SellingMode: "Broker"
        },
    "Both trade panel selections survive returning to the campaign independently"
);
Check(
    tradingSelections.ForCampaign(2)
        is {
            BuyingTravellerId: 0,
            BuyingMode: "Broker",
            SellingTravellerId: 0,
            SellingMode: "Broker"
        },
    "Trade selections are isolated by campaign"
);
Check(
    new MTUAssistant.Services.TradingSelectionState().ForCampaign(1).BuyingTravellerId == 0,
    "Trade selections are isolated by user session"
);
var brokerExample = new Character(
    0,
    1,
    "",
    "Broker",
    "",
    "7777A7",
    "",
    0,
    0,
    0,
    Skills: [new CharacterSkill(0, 0, "Broker", 1), new CharacterSkill(0, 0, "Streetwise", 0)]
);
Check(
    CharacterService.TradingModifier([brokerExample with { Upp = "" }], "Broker") == 1
        && CharacterService.TradingModifier([brokerExample with { Upp = "??????" }], "Broker") == 1
        && CharacterService.TradingModifier([brokerExample with { Upp = "777?A7" }], "Broker") == 2,
    "Unknown trading characteristics default to seven while known values are retained"
);
Check(
    CharacterService.TradingModifier([brokerExample with { Upp = "", Skills = [] }], "Streetwise")
        == -3,
    "Unknown UPP still applies untrained skill penalty"
);
var jackTrader = brokerExample with
{
    Skills = [new CharacterSkill(0, 0, "Jack-of-All-Trades", 2)],
};
Check(
    CharacterService.SkillDm(jackTrader, "Broker") == -1
        && CharacterService.TradingModifier([jackTrader], "Broker") == 0
        && CharacterService.TradingModifier([jackTrader], "Streetwise") == 0,
    "JoaT 2 reduces both untrained trade penalties on the same Traveller"
);
foreach (var level in new[] { 0, 1, 2, 3, 5 })
    Check(
        CharacterService.SkillDm(
            jackTrader with
            {
                Skills = [new CharacterSkill(0, 0, "jack-of-all-trades", level)],
            },
            "Broker"
        )
            == -3 + Math.Min(3, level),
        "JoaT penalty reduction is capped and case insensitive"
    );
Check(
    CharacterService.SkillDm(
        jackTrader with
        {
            Skills =
            [
                new CharacterSkill(0, 0, "Broker", 0),
                new CharacterSkill(0, 0, "Jack-of-All-Trades", 3),
            ],
        },
        "Broker"
    ) == 0
        && CharacterService.SkillDm(
            jackTrader with
            {
                Skills =
                [
                    new CharacterSkill(0, 0, "Broker", 2),
                    new CharacterSkill(0, 0, "Jack-of-All-Trades", 3),
                ],
            },
            "Broker"
        ) == 2,
    "JoaT never boosts trained skills including level zero"
);
Check(
    CharacterService.TradingModifier(
        [brokerExample, jackTrader with { Inactive = true, Upp = "777FF7" }],
        "Streetwise"
    ) == 1,
    "Inactive JoaT user excluded"
);
var smartTrader = brokerExample with
{
    Upp = "777C77",
    Skills = [new CharacterSkill(0, 0, "broker", 0), new CharacterSkill(0, 0, "Streetwise", 3)],
};
Check(
    CharacterService.TradingModifier([brokerExample], "Broker") == 2,
    "Broker 1 plus EDU 10 gives +2"
);
Check(
    CharacterService.TradingModifier([smartTrader], "Broker") == 2,
    "Higher INT used with skill zero and case insensitive skill name"
);
Check(
    CharacterService.TradingModifier([brokerExample, smartTrader], "Streetwise") == 5,
    "Streetwise uses its own best total"
);
Check(
    CharacterService.TradingModifier(
        [brokerExample, smartTrader with { Inactive = true }],
        "Streetwise"
    ) == 1,
    "Inactive high skill excluded"
);
Check(
    CharacterService.TradingModifier([brokerExample with { Skills = [] }], "Broker") == -2,
    "Untrained trading penalty added to best characteristic"
);
Check(
    CharacterService.TradingModifier(
        [brokerExample with { Upp = "777227", Skills = [new CharacterSkill(0, 0, "Broker", 0)] }],
        "Broker"
    ) == -2,
    "Negative totals are retained"
);
Check(
    CharacterService.TradingModifier([], "Broker") is null
        && CharacterService.TradingModifier([brokerExample with { Inactive = true }], "Broker")
            is null,
    "No active Traveller cannot supply a trading DM"
);

// Character naming must preserve existing player and NPC data in both directions.
using (var renameConnection = new SqliteConnection("Data Source=:memory:"))
{
    await renameConnection.OpenAsync();
    var options = new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(renameConnection).Options;
    await using var renameDb = new MtuDbContext(options);
    var migrator = renameDb.GetService<IMigrator>();
    const string previous = "20261103023637_PatronsAndEnemies";
    await migrator.MigrateAsync(previous);
    await renameDb.Database.ExecuteSqlRawAsync(
        """
        INSERT INTO Campaigns (Id, Name, Day, Year, IsActive) VALUES (1, 'Rename fixture', 1, 1105, 1);
        INSERT INTO Travellers (Id, CampaignId, PlayerName, TravellerName, Race, Upp, TermsServed, HighestNavalRank, HighestScoutRank, NaturalDr, Inactive, IsNpc, TokenImage)
          VALUES (1, 1, 'Player', 'Player character', 'Human', '777777', 'Navy 2', 2, 0, 1, 1, 0, X'89504E470D0A1A0A'),
                 (2, 1, '', 'NPC character', 'Vargr', '888888', 'Scout 1', 0, 1, 2, 0, 1, X'89504E470D0A1A0A');
        INSERT INTO PatronsAndEnemies (TravellerId, Occupation, Gender, Quirks1, Quirks2, SkillLevel) VALUES (2, 'Fixer', 'Female', 'Alert', 'Quiet', 3);
        INSERT INTO TravellerSkills (Id, TravellerId, Name, Level) VALUES (1, 1, 'Broker', 2), (2, 2, 'Streetwise', 3);
        INSERT INTO Equipment (Id, Kind, Name, Protection, Tl, Rad, Kg, Cost, RequiredSkill, Range, Damage, Magazine, MagCost, Traits, Notes)
          VALUES (1, 1, 'Fixture rifle', 0, 8, 0, '4', '500', '', 100, '3D', '20', '10', 'Auto 2', 'Preserve');
        INSERT INTO TravellerEquipment (TravellerId, EquipmentId, Quantity, Equipped, Scope, TargetingSystem, LaserSight, HolographicSight, Fof, Phud, SmartTracker)
          VALUES (1, 1, 2, 1, 1, 1, 1, 1, 1, 1, 1), (2, 1, 3, 1, 1, 1, 1, 1, 1, 1, 1);
        """
    );
    for (var pass = 0; pass < 2; pass++)
    {
        await migrator.MigrateAsync();
        var characters = new CharacterService(renameDb);
        var player = (await characters.ListAsync(1)).Single();
        var npc = (await new NpcService(renameDb, characters).ListAsync(1)).Single();
        Check(
            player.Id == 1
                && player.Name == "Player character"
                && player.Inactive
                && player.TermsServed == "Navy 2"
                && player.TokenImage is { Length: 8 }
                && player.Skills!.Single() is { Id: 1, Name: "Broker", Level: 2 },
            "Rename preserves player details and skills"
        );
        Check(
            npc.Character.Id == 2
                && npc.Character.IsNpc
                && npc.Character.Name == "NPC character"
                && npc.Occupation == "Fixer"
                && npc.Gender == "Female"
                && npc.Quirks1 == "Alert"
                && npc.Quirks2 == "Quiet"
                && npc.SkillLevel == NpcSkillLevel.Practitioner
                && npc.Character.Skills!.Single().Level == 3,
            "Rename preserves NPC profile and skills"
        );
        foreach (var id in new[] { 1, 2 })
        {
            var equipment = (
                await new EquipmentService(renameDb).ListDetailsForCharacterAsync(1, id)
            ).Single();
            Check(
                equipment.Quantity == id + 1
                    && equipment.Equipped
                    && equipment.Scope
                    && equipment.TargetingSystem
                    && equipment.LaserSight
                    && equipment.HolographicSight
                    && equipment.Fof
                    && equipment.Phud
                    && equipment.SmartTracker,
                "Rename preserves quantities, equipped state, and every attachment"
            );
        }
        Check(
            await renameDb
                .Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_foreign_key_check")
                .SingleAsync() == 0,
            "Renamed foreign keys remain valid"
        );
        Check(
            await renameDb
                .Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name IN ('Travellers','TravellerSkills','TravellerEquipment','PatronsAndEnemies')"
                )
                .SingleAsync() == 0,
            "Only new character table names remain"
        );
        if (pass == 0)
        {
            await migrator.MigrateAsync(previous);
            Check(
                await renameDb
                    .Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM Travellers")
                    .SingleAsync() == 2,
                "Rollback preserves both characters under original names"
            );
        }
    }
    await renameDb.Database.ExecuteSqlRawAsync("DELETE FROM Characters WHERE Id=2");
    Check(
        !await renameDb.NPCs.AnyAsync()
            && await renameDb.CharacterSkills.CountAsync() == 1
            && await renameDb.CharacterEquipment.CountAsync() == 1,
        "Renamed character deletion cascades through NPC profile, skills and equipment"
    );
}

// Upgrade an existing numeric Terms Served value without losing Traveller skills or token.
using (var termsConnection = new SqliteConnection("Data Source=:memory:"))
{
    await termsConnection.OpenAsync();
    var termsOptions = new DbContextOptionsBuilder<MtuDbContext>()
        .UseSqlite(termsConnection)
        .Options;
    await using var termsDb = new MtuDbContext(termsOptions);
    var termsMigrator = termsDb.GetService<IMigrator>();
    await termsMigrator.MigrateAsync("20261102103310_TravellerToken");
    await termsDb.Database.ExecuteSqlRawAsync(
        "INSERT INTO Campaigns (Id, Name, Day, Year, IsActive) VALUES (1, 'Old', 1, 1105, 1)"
    );
    await termsDb.Database.ExecuteSqlRawAsync(
        "INSERT INTO Travellers (Id, CampaignId, PlayerName, TravellerName, Race, Upp, TermsServed, HighestNavalRank, HighestScoutRank, NaturalDr, TokenImage) VALUES (1, 1, 'P', 'T', '', '', 3, 0, 0, 0, X'89504E470D0A1A0A')"
    );
    await termsDb.Database.ExecuteSqlRawAsync(
        "INSERT INTO TravellerSkills (Id, TravellerId, Name, Level) VALUES (1, 1, 'Pilot', 2)"
    );
    await termsMigrator.MigrateAsync();
    var upgraded = (await new CharacterService(termsDb).ListAsync(1)).Single();
    Check(!upgraded.Inactive, "Existing Travellers default to active after migration");
    Check(
        upgraded.TermsServed == "3"
            && upgraded.TokenImage is { Length: 8 }
            && upgraded.Skills!.Single() is { Name: "Pilot", Level: 2 },
        "Terms migration preserves numeric value, token and skills"
    );
    await new CharacterService(termsDb).SaveAsync(
        upgraded with
        {
            TermsServed = "Navy 3, Diplomat 1",
        }
    );
    Check(
        (await new CharacterService(termsDb).ListAsync(1)).Single().TermsServed
            == "Navy 3, Diplomat 1",
        "Text terms persist"
    );
}

// Migrate numeric text ranges to REAL without losing item details or assignments.
using (var rangeConnection = new SqliteConnection("Data Source=:memory:"))
{
    await rangeConnection.OpenAsync();
    var rangeOptions = new DbContextOptionsBuilder<MtuDbContext>()
        .UseSqlite(rangeConnection)
        .Options;
    await using var rangeDb = new MtuDbContext(rangeOptions);
    var rangeMigrator = rangeDb.GetService<IMigrator>();
    await rangeMigrator.MigrateAsync("20261102214045_Equipment");
    await rangeDb.Database.ExecuteSqlRawAsync(
        """
        INSERT INTO Campaigns (Id, Name, Day, Year, IsActive) VALUES (1, 'Range migration', 1, 1105, 1);
        INSERT INTO Travellers (Id, CampaignId, PlayerName, TravellerName, Race, Upp, TermsServed, HighestNavalRank, HighestScoutRank, NaturalDr, Inactive)
            VALUES (1, 1, '', 'Test Traveller', '', '777777', '0', 0, 0, 0, 0);
        INSERT INTO Equipment (Id, Kind, Name, Protection, Tl, Rad, Kg, Cost, RequiredSkill, Range, Damage, Magazine, MagCost, Traits, Notes)
            VALUES (1, 1, 'Test rifle', 0, 7, 0, '4.5', '500', '', '150.5', '3D', '20', '10', 'Auto 2', 'Keep these notes'),
                   (2, 0, 'Test armour', 8, 10, 5, '2.5', '100', 'Vacc Suit 0', '', '', '', '0', '', 'Keep armour notes');
        INSERT INTO TravellerEquipment (TravellerId, EquipmentId) VALUES (1, 1), (1, 2);
        """
    );
    await rangeMigrator.MigrateAsync();
    var migratedEquipment = await new EquipmentService(rangeDb).ListForCharacterAsync(1, 1);
    Check(
        migratedEquipment.Count == 2
            && migratedEquipment.Single(e => e.Id == 1)
                is { Range: 150.5, Damage: "3D", Magazine: "20", Notes: "Keep these notes" }
            && migratedEquipment.Single(e => e.Id == 2)
                is { Range: 0, Protection: 8, Notes: "Keep armour notes" },
        "Range migration preserves fractional numeric ranges, armour and Traveller assignments"
    );
    Check(
        await rangeDb
            .Database.SqlQueryRaw<string>(
                "SELECT type AS Value FROM pragma_table_info('Equipment') WHERE name = 'Range'"
            )
            .SingleAsync() == "REAL",
        "Range column has numeric SQLite affinity"
    );
    var variantStore = new EquipmentService(rangeDb);
    var highTechArmour = await variantStore.SaveAsync(
        new EquipmentItem
        {
            Name = "Test armour",
            Kind = EquipmentKind.Armour,
            Tl = 12,
        }
    );
    var lowTechArmour = await variantStore.SaveAsync(
        new EquipmentItem
        {
            Name = "Test armour",
            Kind = EquipmentKind.Armour,
            Tl = 9,
        }
    );
    await variantStore.SaveAsync(
        new EquipmentItem
        {
            Name = "A suit",
            Kind = EquipmentKind.Armour,
            Tl = 13,
        }
    );
    await variantStore.SaveAsync(
        new EquipmentItem
        {
            Name = "Test armour",
            Kind = EquipmentKind.Weapon,
            Tl = 10,
        }
    );
    Check(
        (await variantStore.ListAsync())
            .Where(e => e.Kind == EquipmentKind.Armour)
            .Select(e => (e.Name, e.Tl))
            .SequenceEqual(
                new[]
                {
                    ("A suit", 13),
                    ("Test armour", 9),
                    ("Test armour", 10),
                    ("Test armour", 12),
                }
            ),
        "Equipment catalogue sorts Name then TL and allows TL variants"
    );
    await variantStore.AddToCharacterAsync(1, 1, highTechArmour.Id);
    await variantStore.AddToCharacterAsync(1, 1, lowTechArmour.Id);
    Check(
        (await variantStore.ListForCharacterAsync(1, 1))
            .Where(e => e.Kind == EquipmentKind.Armour)
            .Select(e => e.Tl)
            .SequenceEqual(new[] { 9, 10, 12 }),
        "Traveller equipment table sorts same-name TL variants"
    );
    Check(
        (await new CharacterService(rangeDb).ListAsync(1))
            .Single()
            .Equipment!.Where(e => e.Kind == EquipmentKind.Armour)
            .Select(e => e.Tl)
            .SequenceEqual(new[] { 9, 10, 12 }),
        "Traveller summaries use Name then TL order"
    );
    try
    {
        await variantStore.SaveAsync(
            new EquipmentItem
            {
                Name = "Test armour",
                Kind = EquipmentKind.Armour,
                Tl = 10,
            }
        );
        throw new Exception("Duplicate equipment variant accepted");
    }
    catch (ArgumentException) { }
    var duplicateEdit = highTechArmour.Copy();
    duplicateEdit.Tl = 10;
    try
    {
        await variantStore.SaveAsync(duplicateEdit);
        throw new Exception("Duplicate variant edit accepted");
    }
    catch (ArgumentException) { }
    Check(
        (await variantStore.ListAsync()).Single(e => e.Id == highTechArmour.Id).Tl == 12,
        "Rejected duplicate edit preserves existing variant"
    );
    await variantStore.SaveAsync(highTechArmour);
    try
    {
        await rangeDb.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO Equipment (Kind, Name, Protection, Tl, Rad, Kg, Cost, RequiredSkill, Range, Damage, Magazine, MagCost, Traits, Notes)
            SELECT Kind, Name, Protection, Tl, Rad, Kg, Cost, RequiredSkill, Range, Damage, Magazine, MagCost, Traits, Notes FROM Equipment WHERE Id = 2;
            """
        );
        throw new Exception("Database accepted duplicate equipment variant");
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { }
    Check(
        migratedEquipment.All(e =>
            e.ProtectionL is null
            && e.ProtectionE is null
            && e.ProtectionP is null
            && e.ProtectionK is null
        ),
        "Existing equipment defaults optional protections to null during migration"
    );
    var damageArmour = highTechArmour.Copy();
    damageArmour.Protection = 8;
    damageArmour.ProtectionL = 2;
    damageArmour.ProtectionE = 4;
    damageArmour.ProtectionP = 0;
    damageArmour.ProtectionK = 6;
    await variantStore.SaveAsync(damageArmour);
    await using (var reopenedProtectionDb = new MtuDbContext(rangeOptions))
    {
        var restored = (
            await new EquipmentService(reopenedProtectionDb).ListForCharacterAsync(1, 1)
        ).Single(e => e.Id == highTechArmour.Id);
        Check(
            restored
                is {
                    Protection: 8,
                    ProtectionL: 2,
                    ProtectionE: 4,
                    ProtectionP: 0,
                    ProtectionK: 6
                },
            "All optional armour protections persist including explicit zero"
        );
        Check(
            restored.FormatProtection()
                == "+8, +2 vs Lasers, +4 vs Energy, +0 vs Psionics, +6 vs Kinetic",
            "Armour protection display includes base and supplied damage-specific values"
        );
    }
    damageArmour.ProtectionL =
        damageArmour.ProtectionE =
        damageArmour.ProtectionP =
        damageArmour.ProtectionK =
            null;
    await variantStore.SaveAsync(damageArmour);
    await using (var reopenedProtectionDb = new MtuDbContext(rangeOptions))
    {
        var cleared = (await new EquipmentService(reopenedProtectionDb).ListAsync()).Single(e =>
            e.Id == highTechArmour.Id
        );
        Check(
            cleared.ProtectionL is null
                && cleared.ProtectionE is null
                && cleared.ProtectionP is null
                && cleared.ProtectionK is null
                && cleared.FormatProtection() == "+8",
            "Optional protections can be cleared and omitted from display"
        );
    }
    Check(new EquipmentItem().FormatProtection() == "+0", "Zero base protection keeps plus prefix");
    foreach (
        var field in new[]
        {
            nameof(EquipmentItem.ProtectionL),
            nameof(EquipmentItem.ProtectionE),
            nameof(EquipmentItem.ProtectionP),
            nameof(EquipmentItem.ProtectionK),
        }
    )
    {
        var invalidProtection = damageArmour.Copy();
        typeof(EquipmentItem).GetProperty(field)!.SetValue(invalidProtection, -1);
        try
        {
            await variantStore.SaveAsync(invalidProtection);
            throw new Exception("Negative optional protection accepted");
        }
        catch (ArgumentException) { }
    }
    var weaponWithProtection = new EquipmentItem
    {
        Kind = EquipmentKind.Weapon,
        Name = "Weapon ignores armour fields",
        ProtectionL = 2,
        ProtectionE = 4,
        ProtectionP = 0,
        ProtectionK = 6,
    };
    var cleanWeapon = await variantStore.SaveAsync(weaponWithProtection);
    Check(
        cleanWeapon.ProtectionL is null
            && cleanWeapon.ProtectionE is null
            && cleanWeapon.ProtectionP is null
            && cleanWeapon.ProtectionK is null,
        "Weapon saves clear armour-only protection fields"
    );
    var defaultLoadout = await variantStore.ListDetailsForCharacterAsync(1, 1);
    Check(
        defaultLoadout.All(e =>
            e.Quantity == 1
            && !e.Scope
            && !e.TargetingSystem
            && !e.LaserSight
            && !e.HolographicSight
            && !e.Fof
            && !e.Phud
            && !e.SmartTracker
        ),
        "Migration gives existing equipped items quantity one and no attachments"
    );
    Check(
        defaultLoadout.All(e => !e.Equipped),
        "Existing equipment migrates to unchecked worn/equipped status"
    );
    var configuredRifle = defaultLoadout.Single(e => e.Equipment.Id == 1).Copy();
    configuredRifle.Quantity = 3;
    configuredRifle.Equipped = true;
    configuredRifle.Scope =
        configuredRifle.TargetingSystem =
        configuredRifle.LaserSight =
        configuredRifle.HolographicSight =
        configuredRifle.Fof =
        configuredRifle.Phud =
        configuredRifle.SmartTracker =
            true;
    await variantStore.SaveCharacterDetailsAsync(1, 1, configuredRifle);
    await using (var reopenedOptionsDb = new MtuDbContext(rangeOptions))
    {
        var restored = (
            await new EquipmentService(reopenedOptionsDb).ListDetailsForCharacterAsync(1, 1)
        ).Single(e => e.Equipment.Id == 1);
        Check(
            restored.Quantity == 3
                && restored.Scope
                && restored.TargetingSystem
                && restored.LaserSight
                && restored.HolographicSight
                && restored.Fof
                && restored.Phud
                && restored.SmartTracker
                && restored.IntelligentWeapon,
            "All equipped item options and quantity persist; Smart Tracker implies Intelligent Weapon"
        );
        Check(
            restored.AttachmentSummary
                == "Scope, Targeting System, Laser Sights, Holographic Sights, FOF, P-HUD, Smart Tracker (Intelligent Weapon)"
                && restored.Equipment.Traits == "Auto 2",
            "Attachment summary is brief and preserves catalogue traits"
        );
    }
    await variantStore.SetEquippedAsync(1, 1, 1, false);
    await using (var reopenedEquippedDb = new MtuDbContext(rangeOptions))
    {
        var unequipped = (
            await new EquipmentService(reopenedEquippedDb).ListDetailsForCharacterAsync(1, 1)
        ).Single(e => e.Equipment.Id == 1);
        Check(
            !unequipped.Equipped
                && unequipped.Quantity == 3
                && unequipped.Scope
                && unequipped.SmartTracker,
            "Unequipping persists without changing quantity or attachments"
        );
    }
    await variantStore.SetEquippedAsync(1, 1, 1, true);
    await variantStore.SetEquippedAsync(1, 1, 2, true);
    var stillEquipped = (await variantStore.ListDetailsForCharacterAsync(1, 1))
        .Single(e => e.Equipment.Id == 1)
        .Copy();
    await variantStore.SaveCharacterDetailsAsync(1, 1, stillEquipped);
    await using (var reopenedEquippedDb = new MtuDbContext(rangeOptions))
    {
        var equipped = await new EquipmentService(reopenedEquippedDb).ListDetailsForCharacterAsync(
            1,
            1
        );
        Check(
            equipped.Single(e => e.Equipment.Id == 1).Equipped
                && equipped.Single(e => e.Equipment.Id == 2).Equipped,
            "Worn armour and equipped weapon flags persist; options edits preserve status"
        );
    }
    try
    {
        await variantStore.SetEquippedAsync(int.MaxValue, 1, 1, false);
        throw new Exception("Cross-campaign equipped update accepted");
    }
    catch (ArgumentException) { }
    try
    {
        await variantStore.SetEquippedAsync(1, 1, int.MaxValue, true);
        throw new Exception("Missing equipment equipped update accepted");
    }
    catch (ArgumentException) { }
    var optionsTravellers = new CharacterService(rangeDb);
    await optionsTravellers.SaveAsync(
        new Character(0, 1, "", "Options Other", "", "777777", "0", 0, 0, 0)
    );
    var optionsOther = (await optionsTravellers.ListAsync(1)).Single(t =>
        t.Name == "Options Other"
    );
    await variantStore.AddToCharacterAsync(
        1,
        optionsOther.Id,
        new CharacterEquipmentDetails
        {
            Equipment = configuredRifle.Equipment,
            Quantity = 2,
            Scope = true,
        }
    );
    var otherLoadout = (
        await variantStore.ListDetailsForCharacterAsync(1, optionsOther.Id)
    ).Single();
    Check(
        otherLoadout.Quantity == 2
            && !otherLoadout.Equipped
            && otherLoadout.AttachmentSummary == "Scope"
            && !otherLoadout.IntelligentWeapon
            && (await variantStore.ListDetailsForCharacterAsync(1, 1))
                .Single(e => e.Equipment.Id == 1)
                .Quantity == 3,
        "Quantity and attachments are independent per Traveller"
    );
    var cancelledOptions = otherLoadout.Copy();
    cancelledOptions.Quantity = 99;
    cancelledOptions.SmartTracker = true;
    Check(
        (await variantStore.ListDetailsForCharacterAsync(1, optionsOther.Id)).Single().Quantity == 2
            && !otherLoadout.SmartTracker,
        "Editing a copy does not save changes before confirmation"
    );
    foreach (var quantity in new[] { 0, -1 })
    {
        var invalidQuantity = otherLoadout.Copy();
        invalidQuantity.Quantity = quantity;
        try
        {
            await variantStore.SaveCharacterDetailsAsync(1, optionsOther.Id, invalidQuantity);
            throw new Exception("Invalid equipped quantity accepted");
        }
        catch (ArgumentException) { }
    }
    try
    {
        await variantStore.SaveCharacterDetailsAsync(int.MaxValue, 1, configuredRifle);
        throw new Exception("Cross-campaign options edit accepted");
    }
    catch (ArgumentException) { }
    var melee = await variantStore.SaveAsync(
        new EquipmentItem
        {
            Name = "Melee test",
            Kind = EquipmentKind.Weapon,
            Range = 0,
            Damage = "1D",
        }
    );
    try
    {
        await variantStore.AddToCharacterAsync(
            1,
            1,
            new CharacterEquipmentDetails { Equipment = melee, Scope = true }
        );
        throw new Exception("Melee attachments accepted");
    }
    catch (ArgumentException) { }
    Check(
        !(await variantStore.ListForCharacterAsync(1, 1)).Any(e => e.Id == melee.Id),
        "Invalid equipped options do not add an assignment"
    );
    await variantStore.AddToCharacterAsync(
        1,
        1,
        new CharacterEquipmentDetails { Equipment = melee, Quantity = 4 }
    );
    Check(
        (await variantStore.ListDetailsForCharacterAsync(1, 1)).Single(e =>
            e.Equipment.Id == melee.Id
        )
            is {
                Quantity: 4,
                SupportsAttachments: false,
                AttachmentSummary: "",
                IntelligentWeapon: false
            },
        "Melee weapons support quantity without attachment options"
    );
    var forgedArmour = new EquipmentItem
    {
        Id = 2,
        Kind = EquipmentKind.Weapon,
        Range = 100,
    };
    try
    {
        await variantStore.SaveCharacterDetailsAsync(
            1,
            1,
            new CharacterEquipmentDetails { Equipment = forgedArmour, LaserSight = true }
        );
        throw new Exception("Armour attachments accepted via forged category");
    }
    catch (ArgumentException) { }
    otherLoadout.Scope = false;
    await variantStore.SaveCharacterDetailsAsync(1, optionsOther.Id, otherLoadout);
    Check(
        (await variantStore.ListDetailsForCharacterAsync(1, optionsOther.Id)).Single()
            is { Quantity: 2, AttachmentSummary: "", Scope: false },
        "Attachment options can be cleared"
    );
    var npcTravellers = new CharacterService(rangeDb);
    var npcStore = new NpcService(rangeDb, npcTravellers);
    var playerCountBeforeNpc = (await npcTravellers.ListAsync(1)).Count;
    var mailBeforeNpc = await npcTravellers.MailModifiersAsync(1);
    var stewardBeforeNpc = await npcTravellers.StewardModifierAsync(1);
    var tradeBeforeNpc = CharacterService.TradingModifier(
        await npcTravellers.ListAsync(1),
        "Broker"
    );
    var npcToken = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+nL1sAAAAASUVORK5CYII="
    );
    await npcStore.SaveAsync(
        new Npc(
            new Character(0, 1, "", " Zana ", " Human ", "", "0", 0, 0, 0, TokenImage: npcToken),
            " Broker ",
            " Female ",
            " Always smiling ",
            " Counts every credit "
        )
    );
    var npc = (await npcStore.ListAsync(1)).Single();
    Check(
        npc
            is {
                Occupation: "Broker",
                Gender: "Female",
                Quirks1: "Always smiling",
                Quirks2: "Counts every credit",
                SkillLevel: NpcSkillLevel.Novice
            }
            && npc.Character is { Name: "Zana", Race: "Human", Upp: "777777", IsNpc: true },
        "Patron/enemy fields trim and default UPP without creating a player Traveller"
    );
    await npcStore.SaveAsync(npc with { Character = npc.Character with { Upp = "a98765" } });
    Check(
        (await npcStore.ListAsync(1)).Single().Character.Upp == "A98765",
        "Patron/enemy UPP is uppercase"
    );
    await npcStore.SaveAsync(npc with { Character = npc.Character with { Upp = "FFFFFF" } });
    await npcTravellers.SaveSkillAsync(1, new CharacterSkill(0, npc.Character.Id, "Steward", 5));
    await npcTravellers.SaveSkillAsync(1, new CharacterSkill(0, npc.Character.Id, "Broker", 5));
    await variantStore.AddToCharacterAsync(
        1,
        npc.Character.Id,
        new CharacterEquipmentDetails
        {
            Equipment = configuredRifle.Equipment,
            Quantity = 2,
            Scope = true,
            Equipped = true,
        }
    );
    foreach (var skillRating in Enum.GetValues<NpcSkillLevel>())
    {
        npc = (await npcStore.ListAsync(1)).Single();
        await npcStore.SaveAsync(npc with { SkillLevel = skillRating });
        Check(
            (await npcStore.ListAsync(1)).Single().SkillLevel == skillRating,
            "All NPC skill levels persist without generating skills"
        );
    }
    await using (var reopenedNpcDb = new MtuDbContext(rangeOptions))
    {
        var restoredNpc = (
            await new NpcService(reopenedNpcDb, new CharacterService(reopenedNpcDb)).ListAsync(1)
        ).Single();
        Check(
            restoredNpc.Character.TokenImage!.SequenceEqual(npcToken)
                && restoredNpc.Character.Skills!.Count == 2
                && restoredNpc.Character.Equipment!.Single().Id == 1,
            "NPC token, shared skills and equipment summaries persist across contexts"
        );
        Check(
            (
                await new EquipmentService(reopenedNpcDb).ListDetailsForCharacterAsync(
                    1,
                    npc.Character.Id
                )
            ).Single()
                is { Quantity: 2, Scope: true, Equipped: true },
            "NPC equipment reuses quantity, attachments and equipped state"
        );
    }
    Check(
        (await npcTravellers.ListAsync(1)).Count == playerCountBeforeNpc
            && (await npcStore.ListAsync(int.MaxValue)).Count == 0,
        "Patrons and enemies are separate from player Travellers and other campaigns"
    );
    Check(
        await npcTravellers.MailModifiersAsync(1) == mailBeforeNpc
            && (await npcTravellers.StewardModifierAsync(1)).Value == stewardBeforeNpc.Value
            && CharacterService.TradingModifier(
                (await npcTravellers.ListAsync(1)).Append(
                    (await npcStore.ListAsync(1)).Single().Character
                ),
                "Broker"
            ) == tradeBeforeNpc,
        "NPC ranks, characteristics and skills never affect party transport or trading modifiers"
    );
    var npcCampaigns = new CampaignService(rangeDb);
    await npcCampaigns.SaveAsync(new Campaign(0, "NPC other campaign", 1, 1105, null, null, false));
    var npcOtherCampaign = (await npcCampaigns.ListAsync()).Single(c =>
        c.Name == "NPC other campaign"
    );
    try
    {
        await npcStore.SaveAsync(
            npc with
            {
                Character = npc.Character with { CampaignId = npcOtherCampaign.Id },
            }
        );
        throw new Exception("Cross-campaign NPC edit accepted");
    }
    catch (ArgumentException) { }
    try
    {
        await npcStore.DeleteAsync(npcOtherCampaign.Id, npc.Character.Id);
        throw new Exception("Cross-campaign NPC deletion accepted");
    }
    catch (ArgumentException) { }
    try
    {
        await npcStore.SaveAsync(npc with { Character = npc.Character with { Id = 1 } });
        throw new Exception("Player edited through NPC service");
    }
    catch (ArgumentException) { }
    try
    {
        await npcTravellers.SaveAsync(npc.Character);
        throw new Exception("NPC edited through player service");
    }
    catch (InvalidOperationException) { }
    try
    {
        await npcTravellers.DeleteAsync(1, npc.Character.Id);
        throw new Exception("NPC deleted through player service");
    }
    catch (InvalidOperationException) { }
    try
    {
        await npcStore.SaveAsync(npc with { SkillLevel = (NpcSkillLevel)999 });
        throw new Exception("Invalid NPC skill level accepted");
    }
    catch (ArgumentException) { }
    await npcStore.DeleteAsync(1, npc.Character.Id);
    Check(
        !await rangeDb.NPCs.AnyAsync(p => p.CharacterId == npc.Character.Id)
            && !await rangeDb.CharacterSkills.AnyAsync(p => p.CharacterId == npc.Character.Id)
            && !await rangeDb.CharacterEquipment.AnyAsync(p => p.CharacterId == npc.Character.Id)
            && await rangeDb.Equipment.AnyAsync(e => e.Id == 1),
        "Deleting NPC cascades skills and equipment assignments while preserving catalogue"
    );
    await npcStore.SaveAsync(
        new Npc(
            new Character(0, npcOtherCampaign.Id, "", "Other patron", "", "777777", "0", 0, 0, 0)
        )
    );
    await npcCampaigns.DeleteAsync(npcOtherCampaign.Id);
    Check(
        !await rangeDb.NPCs.AnyAsync()
            && (await npcTravellers.ListAsync(1)).Count == playerCountBeforeNpc,
        "Deleting NPC campaign preserves other campaign Travellers"
    );
}

// Upgrade an existing database, then prove repeated refresh replaces metadata atomically.
using (var importConnection = new SqliteConnection("Data Source=:memory:"))
{
    await importConnection.OpenAsync();
    var options = new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(importConnection).Options;
    await using var db = new MtuDbContext(options);
    var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync("20261020100154_InitialCreate");
    await migrator.MigrateAsync();
    var summary = new SectorSummary
    {
        X = -4,
        Y = -1,
        Milieu = "M1105",
        Names = [new SectorName { Text = "Spinward Marches" }],
    };
    var mock = new RefreshClient(summary);
    var reports = new List<TileRefreshProgress>();
    var worker = new TileRefreshWorker(mock, new SectorImporter(db));
    var progress = new InlineProgress<TileRefreshProgress>(reports.Add);
    await worker.RunAsync(progress);
    var sector = await db.Sectors.SingleAsync();
    var id = sector.Id;
    Check((await db.Borders.SingleAsync()).Color == "lightblue", "Stylesheet border colour");
    var route = await db.Routes.SingleAsync();
    Check(
        route.Color == "#006600" && route.EndOffsetX == 1 && route.End == "0208",
        "Route style and cross-sector offset"
    );
    Check(
        (await db.Allegiances.SingleAsync()).ShortName == "Sword Worlds",
        "Metadata allegiance fields"
    );
    Check((await db.Subsectors.SingleAsync()).Name == "Regina", "Metadata subsector name");
    Check(
        reports.Last() is { Total: 1, Completed: 1, Failed: 0 },
        "Progress counts sectors, not images"
    );
    db.ChangeTracker.Clear();
    var reloadServices = new ServiceCollection();
    reloadServices.AddDbContext<MtuDbContext>(o => o.UseSqlite(importConnection));
    reloadServices.AddMtuCatalogue();
    reloadServices.AddScoped<SectorDataService>();
    using var reloadContainer = reloadServices.BuildServiceProvider();
    var reloadProvider = reloadContainer.GetRequiredService<SystemDataProvider>();
    await reloadProvider.InitializeAsync();
    var sectorData = reloadContainer.GetRequiredService<SectorDataService>();
    var importedStats = await sectorData.GetStatisticsAsync();
    Check(
        importedStats
            is {
                Sectors: 1,
                SectorNames: 1,
                Subsectors: 1,
                Systems: 1,
                Allegiances: 1,
                Borders: 1,
                Routes: 1,
                LastRefreshUtc: null
            }
            && importedStats.Milieus.SequenceEqual(new[] { "M1105" }),
        "Sector data statistics count all imported metadata and systems"
    );
    await db.Systems.ExecuteUpdateAsync(setters =>
        setters
            .SetProperty(s => s.Uwp, "A000800-0")
            .SetProperty(s => s.Pbg, "500")
            .SetProperty(s => s.A, "SwCf")
    );
    var persistedPopulation = (await sectorData.GetStatisticsAsync()).PopulationByMilieu.Single();
    Check(
        persistedPopulation.TotalPopulation == 500_000_000m
            && persistedPopulation.TopPolities.Single().Name == "Sword Worlds"
            && persistedPopulation.UnknownSystems == 0,
        "Sector statistics calculate population from persisted UWP/PBG and sector allegiance metadata"
    );
    var recordedTime = new DateTime(2026, 11, 2, 11, 30, 0, DateTimeKind.Utc);
    await sectorData.RecordCompletedRefreshAsync(recordedTime);
    await using (var statisticsScope = reloadContainer.CreateAsyncScope())
    {
        var persistedStats = await statisticsScope
            .ServiceProvider.GetRequiredService<SectorDataService>()
            .GetStatisticsAsync();
        Check(
            persistedStats.LastRefreshUtc == recordedTime
                && persistedStats.LastRefreshUtc.Value.Kind == DateTimeKind.Utc,
            "Refresh timestamp persists across scopes with UTC restored"
        );
    }
    await sectorData.RecordCompletedRefreshAsync(recordedTime.AddHours(1));
    Check(
        await db.SectorRefreshState.CountAsync() == 1
            && (await sectorData.GetStatisticsAsync()).LastRefreshUtc == recordedTime.AddHours(1),
        "New refresh overwrites singleton timestamp"
    );
    var refreshService = new MTUAssistant.Services.TileRefreshService(
        worker,
        reloadProvider,
        sectorData
    );
    var refreshStarted = DateTime.UtcNow;
    await refreshService.StartAsync();
    var refreshedStats = await sectorData.GetStatisticsAsync();
    Check(
        refreshService.Status == MTUAssistant.Services.TileRefreshStatus.Completed
            && refreshedStats.LastRefreshUtc >= refreshStarted
            && refreshedStats.LastRefreshUtc <= DateTime.UtcNow
            && refreshedStats.Systems == 1,
        "Original refresh action records actual completion time and leaves updated statistics available"
    );
    mock.CancelAtStart = true;
    await refreshService.StartAsync();
    Check(
        refreshService.Status == MTUAssistant.Services.TileRefreshStatus.Cancelled
            && (await sectorData.GetStatisticsAsync()).LastRefreshUtc
                == refreshedStats.LastRefreshUtc,
        "Cancelled refresh retains last completed timestamp"
    );
    mock.CancelAtStart = false;
    mock.FailSectorList = true;
    await refreshService.StartAsync();
    Check(
        refreshService.Status == MTUAssistant.Services.TileRefreshStatus.Faulted
            && (await sectorData.GetStatisticsAsync()).LastRefreshUtc
                == refreshedStats.LastRefreshUtc,
        "Failed refresh retains last completed timestamp"
    );
    mock.FailSectorList = false;
    db.ChangeTracker.Clear();
    var campaignService = new CampaignService(db);
    await campaignService.SaveAsync(new Campaign(0, "First Campaign", 1, 1105, id, "1910", false));
    var firstCampaign = (await campaignService.ListAsync()).Single();
    Check(
        firstCampaign.IsActive
            && firstCampaign.LocationSectorId == id
            && firstCampaign.LocationHex == "1910",
        "New campaign is active with persistent location"
    );
    await campaignService.AdjustDateAsync(firstCampaign.Id, -1);
    Check(
        (await campaignService.ListAsync()).Single() is { Day: 365, Year: 1104 },
        "Subtract one day crosses year"
    );
    await campaignService.AdjustDateAsync(firstCampaign.Id, 7);
    Check(
        (await campaignService.ListAsync()).Single() is { Day: 7, Year: 1105 },
        "Add seven days crosses year"
    );
    await campaignService.AdjustDateAsync(firstCampaign.Id, -7);
    await campaignService.AdjustDateAsync(firstCampaign.Id, 1);
    await using (var dateReopened = new MtuDbContext(options))
        Check(
            (await new CampaignService(dateReopened).ListAsync()).Single()
                is { Day: 1, Year: 1105 },
            "Date adjustment persists across contexts"
        );
    await campaignService.SaveAsync(firstCampaign with { Day = 1, Year = 1 });
    try
    {
        await campaignService.AdjustDateAsync(firstCampaign.Id, -1);
        throw new Exception("Date before year one accepted");
    }
    catch (ArgumentException) { }
    await campaignService.SaveAsync(firstCampaign);
    await campaignService.SaveAsync(
        new Campaign(0, "Second Campaign", 365, 1106, null, null, false)
    );
    var campaignList = await campaignService.ListAsync();
    Check(
        campaignList.Count == 2
            && campaignList.Count(c => c.IsActive) == 1
            && campaignList.Single(c => c.IsActive).Name == "Second Campaign",
        "Creating campaign switches active campaign"
    );
    await campaignService.ActivateAsync(firstCampaign.Id);
    await using (var reopened = new MtuDbContext(options))
    {
        var persisted = (await new CampaignService(reopened).ListAsync()).Single(c => c.IsActive);
        Check(
            persisted.Id == firstCampaign.Id
                && persisted.Day == 1
                && persisted.Year == 1105
                && persisted.LocationHex == "1910",
            "Active campaign survives reopening"
        );
    }
    await campaignService.SaveAsync(firstCampaign with { Name = "Updated Campaign", Day = 100 });
    Check(
        (await campaignService.ListAsync()).Single(c => c.Id == firstCampaign.Id)
            is { Name: "Updated Campaign", Day: 100, IsActive: true },
        "Campaign editing preserves active status"
    );
    try
    {
        await campaignService.SaveAsync(firstCampaign with { Day = 366 });
        throw new Exception("Invalid day accepted");
    }
    catch (ArgumentException) { }
    var characterService = new CharacterService(db);
    var secondCampaign = (await campaignService.ListAsync()).Single(c => c.Id != firstCampaign.Id);
    await characterService.SaveAsync(
        new Character(0, firstCampaign.Id, "Player", "Kira", "Human", "a98765", "3", 2, 1, 4)
    );
    var character = (await characterService.ListAsync(firstCampaign.Id)).Single();
    Check(
        character
            is {
                PlayerName: "Player",
                Name: "Kira",
                Race: "Human",
                Upp: "A98765",
                TermsServed: "3",
                HighestNavalRank: 2,
                HighestScoutRank: 1,
                NaturalDr: 4
            },
        "Traveller fields are saved"
    );
    Check(
        (await characterService.ListAsync(secondCampaign.Id)).Count == 0,
        "Travellers are isolated by campaign"
    );
    Check(
        (await characterService.MailModifiersAsync(secondCampaign.Id)) == (0, null),
        "Empty campaign has no mail modifiers"
    );
    Check(
        new[] { '0', '1', '2', '3', '5', '6', '8', '9', 'B', 'C', 'E', 'F' }
            .Select(c => CharacterService.SocialDm("12345" + c))
            .SequenceEqual(new int?[] { -3, -2, -2, -1, -1, 0, 0, 1, 1, 2, 2, 3 })
            && CharacterService.SocialDm("12345?") is null
            && CharacterService.SocialDm("123") is null,
        "SOC DM boundaries and incomplete UPP"
    );
    await characterService.SaveAsync(
        new Character(0, firstCampaign.Id, "", "Second", "", "12345C", "", 0, 4, 0)
    );
    await characterService.SaveAsync(
        new Character(0, secondCampaign.Id, "", "Other", "", "12345F", "", 9, 0, 0)
    );
    Check(
        (await characterService.MailModifiersAsync(firstCampaign.Id)) == (4, 2),
        "Campaign mail modifiers select highest rank and SOC"
    );
    Check(
        (await characterService.MailModifiersAsync(secondCampaign.Id)) == (9, 3),
        "Mail modifiers exclude other campaigns"
    );
    Check(
        await characterService.StewardModifierAsync(firstCampaign.Id)
            is { Value: -3, HasSkill: false },
        "Unskilled Steward uses highest campaign INT minus three"
    );
    await characterService.SaveSkillAsync(
        firstCampaign.Id,
        new CharacterSkill(0, character.Id, "Jack-of-All-Trades", 2)
    );
    Check(
        await characterService.StewardModifierAsync(firstCampaign.Id)
            is { Value: -1, HasSkill: false },
        "Untrained Steward uses same Traveller JoaT plus INT"
    );
    await characterService.SaveSkillAsync(
        firstCampaign.Id,
        (await characterService.ListSkillsAsync(firstCampaign.Id, character.Id)).Single() with
        {
            Level = 3,
        }
    );
    Check(
        await characterService.StewardModifierAsync(firstCampaign.Id)
            is { Value: 0, HasSkill: false },
        "JoaT cap removes penalty without granting Steward training"
    );
    await characterService.DeleteSkillAsync(
        firstCampaign.Id,
        character.Id,
        (await characterService.ListSkillsAsync(firstCampaign.Id, character.Id)).Single().Id
    );
    var secondTraveller = (await characterService.ListAsync(firstCampaign.Id)).Single(t =>
        t.Name == "Second"
    );
    var otherSteward = (await characterService.ListAsync(secondCampaign.Id)).Single();
    await characterService.SaveAsync(secondTraveller with { Upp = "123C5C" });
    Check(
        await characterService.StewardModifierAsync(firstCampaign.Id)
            is { Value: -1, HasSkill: false },
        "Unskilled Steward uses best INT in group"
    );
    await characterService.SaveAsync(secondTraveller with { Upp = "123?5C" });
    Check(
        (await characterService.StewardModifierAsync(firstCampaign.Id)).MissingInt.SequenceEqual(
            new[] { "Second" }
        ),
        "Unknown unskilled INT is identified"
    );
    await characterService.SaveAsync(secondTraveller);
    await characterService.SaveSkillAsync(
        firstCampaign.Id,
        new CharacterSkill(0, secondTraveller.Id, "Steward", 0)
    );
    await characterService.SaveSkillAsync(
        secondCampaign.Id,
        new CharacterSkill(0, otherSteward.Id, "Steward", 5)
    );
    Check(
        (await characterService.StewardModifierAsync(firstCampaign.Id)).Value == -1,
        "Steward zero and negative INT DM are used together"
    );
    await characterService.SaveSkillAsync(
        firstCampaign.Id,
        new CharacterSkill(0, character.Id, "Steward", 1)
    );
    Check(
        (await characterService.StewardModifierAsync(firstCampaign.Id)).Value == 1,
        "Best same-traveller Steward plus INT excludes other campaigns"
    );
    await characterService.SaveAsync(secondTraveller with { Upp = "123?5C" });
    Check(
        (await characterService.StewardModifierAsync(firstCampaign.Id)).MissingInt.SequenceEqual(
            new[] { "Second" }
        ),
        "Unknown Steward intelligence is identified"
    );
    await characterService.SaveAsync(secondTraveller with { Inactive = true, Upp = "123?5C" });
    using (var reopened = new MtuDbContext(options))
        Check(
            (await new CharacterService(reopened).ListAsync(firstCampaign.Id))
                .Single(t => t.Id == secondTraveller.Id)
                .Inactive,
            "Inactive flag persists while Traveller remains listed"
        );
    Check(
        (await characterService.MailModifiersAsync(firstCampaign.Id)) == (2, -1),
        "Inactive Traveller rank and SOC excluded from freight mail modifiers"
    );
    Check(
        await characterService.StewardModifierAsync(firstCampaign.Id)
            is { Value: 1, HasSkill: true, MissingInt.Count: 0 },
        "Inactive Steward and missing INT excluded from passengers"
    );
    await characterService.SaveAsync(character with { Inactive = true });
    Check(
        (await characterService.MailModifiersAsync(firstCampaign.Id)) == (0, null),
        "All inactive gives no mail modifiers"
    );
    Check(
        await characterService.StewardModifierAsync(firstCampaign.Id)
            is { Value: null, HasSkill: false, MissingInt.Count: 0 },
        "All inactive gives no passenger Steward modifier"
    );
    await characterService.SaveAsync(character);
    Check(
        await characterService.StewardModifierAsync(firstCampaign.Id)
            is { Value: 1, HasSkill: true },
        "Reactivated Traveller contributes again"
    );
    await characterService.DeleteSkillAsync(
        firstCampaign.Id,
        character.Id,
        (await characterService.ListSkillsAsync(firstCampaign.Id, character.Id)).Single().Id
    );
    await characterService.DeleteAsync(
        firstCampaign.Id,
        (await characterService.ListAsync(firstCampaign.Id)).Single(t => t.Name == "Second").Id
    );
    await characterService.DeleteAsync(
        secondCampaign.Id,
        (await characterService.ListAsync(secondCampaign.Id)).Single().Id
    );
    await characterService.SaveSkillAsync(
        firstCampaign.Id,
        new CharacterSkill(0, character.Id, "Pilot (spacecraft)", 0)
    );
    await characterService.SaveSkillAsync(
        firstCampaign.Id,
        new CharacterSkill(0, character.Id, "Broker", 2)
    );
    var savedSkills = await characterService.ListSkillsAsync(firstCampaign.Id, character.Id);
    Check(
        savedSkills.Count == 2
            && savedSkills[0] is { Name: "Broker", Level: 2 }
            && savedSkills[1].Level == 0,
        "Skills save and sort with level zero"
    );
    var listedSkills = (await characterService.ListAsync(firstCampaign.Id)).Single().Skills!;
    Check(
        listedSkills
            .Select(skill => (skill.Name, skill.Level))
            .SequenceEqual(savedSkills.Select(skill => (skill.Name, skill.Level))),
        "Traveller list includes named skill levels"
    );
    try
    {
        await characterService.SaveSkillAsync(
            firstCampaign.Id,
            new CharacterSkill(0, character.Id, "broker", 1)
        );
        throw new Exception("Duplicate skill accepted");
    }
    catch (ArgumentException) { }
    try
    {
        await characterService.SaveSkillAsync(
            secondCampaign.Id,
            new CharacterSkill(0, character.Id, "Steward", 1)
        );
        throw new Exception("Cross-campaign skill accepted");
    }
    catch (ArgumentException) { }
    await characterService.SaveSkillAsync(
        firstCampaign.Id,
        savedSkills[0] with
        {
            Name = "Brokerage",
            Level = 3,
        }
    );
    await using (var reopened = new MtuDbContext(options))
        Check(
            (
                await new CharacterService(reopened).ListSkillsAsync(firstCampaign.Id, character.Id)
            ).Any(skill => skill is { Name: "Brokerage", Level: 3 }),
            "Skill edits persist"
        );
    await characterService.DeleteSkillAsync(firstCampaign.Id, character.Id, savedSkills[1].Id);
    Check(
        (await characterService.ListSkillsAsync(firstCampaign.Id, character.Id)).Count == 1,
        "Skill removal"
    );
    await characterService.SaveAsync(character with { Name = "Kira Updated", NaturalDr = 5 });
    await using (var reopened = new MtuDbContext(options))
        Check(
            (await new CharacterService(reopened).ListAsync(firstCampaign.Id)).Single()
                is { Name: "Kira Updated", NaturalDr: 5 },
            "Traveller edits persist"
        );
    var pngToken = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+nL1sAAAAASUVORK5CYII="
    );
    Check(
        CharacterToken.ContentType(pngToken) == "image/png"
            && CharacterToken.ContentType("not an image"u8.ToArray()) is null,
        "Token file type validation"
    );
    var withToken = (await characterService.ListAsync(firstCampaign.Id)).Single() with
    {
        TokenImage = pngToken,
    };
    await characterService.SaveAsync(withToken);
    await using (var reopened = new MtuDbContext(options))
    {
        var restoredToken = (
            await new CharacterService(reopened).ListAsync(firstCampaign.Id)
        ).Single();
        Check(
            restoredToken.TokenImage!.SequenceEqual(pngToken),
            "Token image persists across database contexts"
        );
        await new CharacterService(reopened).SaveAsync(
            restoredToken with
            {
                PlayerName = "Renamed player",
            }
        );
    }
    var unchangedToken = (await characterService.ListAsync(firstCampaign.Id)).Single();
    Check(
        unchangedToken.TokenImage!.SequenceEqual(pngToken),
        "Editing other fields preserves token"
    );
    await characterService.SaveAsync(unchangedToken with { TokenImage = null });
    Check(
        (await characterService.ListAsync(firstCampaign.Id)).Single().TokenImage is null,
        "Token can be removed"
    );
    try
    {
        await characterService.SaveAsync(unchangedToken with { TokenImage = "bad"u8.ToArray() });
        throw new Exception("Invalid token accepted");
    }
    catch (ArgumentException) { }
    try
    {
        await characterService.SaveAsync(character with { CampaignId = secondCampaign.Id });
        throw new Exception("Cross-campaign edit allowed");
    }
    catch (InvalidOperationException) { }
    await characterService.SaveAsync(
        new Character(0, secondCampaign.Id, "P", "Other", "", "", "0", 0, 0, 0)
    );
    var otherTraveller = (await characterService.ListAsync(secondCampaign.Id)).Single();
    var equipmentStore = new EquipmentService(db);
    var armour = await equipmentStore.SaveAsync(
        new EquipmentItem
        {
            Kind = EquipmentKind.Armour,
            Name = " Cloth ",
            Protection = 8,
            Tl = 10,
            Rad = 5,
            Kg = 2.5m,
            Cost = 500.5m,
            RequiredSkill = "Vacc Suit 0",
            Notes = "First line\nSecond line",
        }
    );
    var weapon = await equipmentStore.SaveAsync(
        new EquipmentItem
        {
            Kind = EquipmentKind.Weapon,
            Name = "Rifle",
            Tl = 7,
            Range = 100,
            Damage = "3D",
            Kg = 4.25m,
            Cost = 200,
            Magazine = "20",
            MagCost = 10.5m,
            Traits = "Auto 2",
            Notes = "Test weapon",
        }
    );
    Check(
        armour.Summary == "Cloth (DR +8)" && weapon.Summary == "Rifle (100m, 3D)",
        "Equipment summary formats"
    );
    var fractionalRangeWeapon = weapon.Copy();
    fractionalRangeWeapon.Range = 1.5;
    Check(
        fractionalRangeWeapon.Summary == "Rifle (1.5m, 3D)",
        "Numeric weapon range retains fractional metres in compact summaries"
    );
    foreach (var invalidRange in new[] { -1d, double.NaN, double.PositiveInfinity })
    {
        try
        {
            await equipmentStore.SaveAsync(
                new EquipmentItem
                {
                    Name = "Invalid range",
                    Kind = EquipmentKind.Weapon,
                    Range = invalidRange,
                }
            );
            throw new Exception("Invalid range accepted");
        }
        catch (ArgumentException) { }
    }
    await equipmentStore.AddToCharacterAsync(firstCampaign.Id, character.Id, armour.Id);
    await equipmentStore.AddToCharacterAsync(firstCampaign.Id, character.Id, weapon.Id);
    await equipmentStore.AddToCharacterAsync(secondCampaign.Id, otherTraveller.Id, armour.Id);
    await using (var reopened = new MtuDbContext(options))
    {
        var restored = (await new EquipmentService(reopened).ListAsync());
        Check(
            restored.Single(e => e.Id == armour.Id)
                is {
                    Name: "Cloth",
                    Protection: 8,
                    Tl: 10,
                    Rad: 5,
                    Kg: 2.5m,
                    Cost: 500.5m,
                    RequiredSkill: "Vacc Suit 0",
                    Notes: "First line\nSecond line"
                },
            "All armour attributes persist"
        );
        Check(
            restored.Single(e => e.Id == weapon.Id)
                is {
                    Tl: 7,
                    Range: 100,
                    Damage: "3D",
                    Kg: 4.25m,
                    Cost: 200,
                    Magazine: "20",
                    MagCost: 10.5m,
                    Traits: "Auto 2",
                    Notes: "Test weapon"
                },
            "All weapon attributes persist"
        );
        Check(
            (await new CharacterService(reopened).ListAsync(firstCampaign.Id))
                .Single()
                .Equipment!.Count == 2,
            "Traveller list contains persisted equipment"
        );
        Check(
            (
                await new EquipmentService(reopened).ListForCharacterAsync(
                    secondCampaign.Id,
                    otherTraveller.Id
                )
            ).Count == 1,
            "Equipment assignments isolated by Traveller"
        );
    }
    try
    {
        await equipmentStore.AddToCharacterAsync(firstCampaign.Id, character.Id, armour.Id);
        throw new Exception("Duplicate equipment accepted");
    }
    catch (ArgumentException) { }
    try
    {
        await equipmentStore.AddToCharacterAsync(secondCampaign.Id, character.Id, weapon.Id);
        throw new Exception("Cross-campaign equipment add accepted");
    }
    catch (ArgumentException) { }
    try
    {
        await equipmentStore.RemoveFromCharacterAsync(secondCampaign.Id, character.Id, armour.Id);
        throw new Exception("Cross-campaign equipment removal accepted");
    }
    catch (ArgumentException) { }
    try
    {
        await equipmentStore.SaveAsync(new EquipmentItem { Name = "" });
        throw new Exception("Blank equipment name accepted");
    }
    catch (ArgumentException) { }
    try
    {
        await equipmentStore.SaveAsync(new EquipmentItem { Name = "Invalid", Kg = -1 });
        throw new Exception("Negative weight accepted");
    }
    catch (ArgumentException) { }
    try
    {
        await equipmentStore.AddToCharacterAsync(firstCampaign.Id, character.Id, int.MaxValue);
        throw new Exception("Missing equipment accepted");
    }
    catch (ArgumentException) { }
    var revisedArmour = armour.Copy();
    revisedArmour.Protection = 10;
    revisedArmour.Notes = "Updated notes";
    await equipmentStore.SaveAsync(revisedArmour);
    Check(
        (await equipmentStore.ListForCharacterAsync(firstCampaign.Id, character.Id))
            .Single(e => e.Id == armour.Id)
            .Protection == 10
            && (await equipmentStore.ListForCharacterAsync(secondCampaign.Id, otherTraveller.Id))
                .Single()
                .Notes == "Updated notes",
        "Catalogue edits update all assigned Travellers"
    );
    await equipmentStore.RemoveFromCharacterAsync(firstCampaign.Id, character.Id, weapon.Id);
    Check(
        (await equipmentStore.ListAsync()).Any(e => e.Id == weapon.Id)
            && (await equipmentStore.ListForCharacterAsync(firstCampaign.Id, character.Id)).Count
                == 1,
        "Traveller removal preserves catalogue item"
    );
    await equipmentStore.AddToCharacterAsync(firstCampaign.Id, character.Id, weapon.Id);
    await equipmentStore.DeleteAsync(weapon.Id);
    Check(
        !await db.CharacterEquipment.AnyAsync(e => e.EquipmentId == weapon.Id),
        "Deleting equipment cascades its assignments"
    );

    await characterService.DeleteAsync(secondCampaign.Id, otherTraveller.Id);
    Check((await characterService.ListAsync(secondCampaign.Id)).Count == 0, "Traveller deletion");
    Check(
        !await db.CharacterEquipment.AnyAsync(e => e.CharacterId == otherTraveller.Id)
            && (await equipmentStore.ListAsync()).Count == 1,
        "Deleting Traveller preserves catalogue and removes assignment"
    );
    await campaignService.DeleteAsync(firstCampaign.Id);
    Check(
        (await campaignService.ListAsync()).Single() is { Name: "Second Campaign", IsActive: true },
        "Deleting active campaign selects another"
    );
    Check(
        (await characterService.ListAsync(firstCampaign.Id)).Count == 0,
        "Deleting a campaign deletes its Travellers"
    );
    Check(
        !await db.CharacterSkills.AnyAsync(s => s.CharacterId == character.Id),
        "Deleting a campaign cascades to Traveller skills"
    );
    Check(
        !await db.CharacterEquipment.AnyAsync() && (await equipmentStore.ListAsync()).Count == 1,
        "Deleting campaign cascades equipment assignments and preserves catalogue"
    );
    await campaignService.DeleteAsync((await campaignService.ListAsync()).Single().Id);
    Check(
        (await campaignService.ListAsync()).Count == 0,
        "Deleting last campaign leaves no active campaign"
    );
    var marketService = new TradingService(db);
    db.TradeMarkets.Add(
        new TradeMarketEntity
        {
            SectorId = id,
            Hex = "0101",
            Mode = "Broker",
            ResultJson =
                "{\"Mode\":\"Broker\",\"SkillDm\":2,\"RolledUtc\":\"2026-01-01T00:00:00Z\",\"RandomRolls\":1,\"Goods\":[{\"D66\":11,\"Name\":\"Common Electronics\",\"Lots\":2,\"Tons\":40,\"Illegal\":false,\"Stolen\":false}]}",
        }
    );
    await db.SaveChangesAsync();
    var legacy = await marketService.GetAsync(id, "0101", "Broker", "In Ht");
    Check(
        legacy is { PricingVersion: 1 }
            && legacy.Goods.Single()
                is {
                    Lots: 2,
                    Tons: 40,
                    BasePrice: 20000,
                    PricePercent: not null,
                    BuyPrice: not null
                },
        "Legacy market priced without rerolling goods"
    );
    Check(
        (await marketService.GetAsync(id, "0101", "Broker", "In Ht"))!.Goods.SequenceEqual(
            legacy!.Goods
        ),
        "Legacy prices are persisted and not rerolled"
    );
    var datedMarket = await marketService.RollAsync(id, "1913", "Broker", 0, "", targetDay: today);
    Check(
        (await marketService.GetAsync(id, "1913", "Broker"))!.TargetDay == today,
        "Buying target date persists"
    );
    var datedSales = await marketService.RollSalesAsync(
        id,
        "1914",
        "Broker",
        0,
        "",
        targetDay: today - 14
    );
    Check(
        (await marketService.GetSalesAsync(id, "1914", "Broker"))!.TargetDay == today - 14,
        "Selling target date persists"
    );
    var zoneSales = await marketService.RollSalesAsync(
        id,
        "1915",
        "Broker",
        0,
        TradePricing.WithZone("", "R"),
        population: 9
    );
    var savedZoneSales = await marketService.GetSalesAsync(id, "1915", "Broker");
    Check(
        savedZoneSales!.TonnageDm == 3
            && savedZoneSales.PopulationCode == 9
            && savedZoneSales.Goods.Single(g => g.D66 == 24).SaleDm == 4,
        "Zone and population results persist with sale market"
    );
    var savedSales = await marketService.RollSalesAsync(id, "1910", "Broker", 3, "In");
    var legacySale = savedSales with
    {
        DemandVersion = 0,
        BuyerDm = null,
        Goods = savedSales.Goods.Select(g => g with { Tons = null }).ToArray(),
    };
    db.TradeMarkets.Add(
        new TradeMarketEntity
        {
            SectorId = id,
            Hex = "1912",
            Mode = "Sale:Broker",
            ResultJson = System.Text.Json.JsonSerializer.Serialize(legacySale),
        }
    );
    await db.SaveChangesAsync();
    var upgradedSale = await marketService.GetSalesAsync(id, "1912", "Broker");
    Check(
        upgradedSale is { IsOutdated: true, DemandVersion: 1, BuyerDm: null }
            && upgradedSale.Goods.All(g => g.Tons > 0),
        "Older sale market gains demand without inventing a buyer DM"
    );
    Check(
        upgradedSale!
            .Goods.Select(g => g.SalePrice)
            .SequenceEqual(legacySale.Goods.Select(g => g.SalePrice)),
        "Demand upgrade preserves saved prices"
    );
    Check(
        (await marketService.GetSalesAsync(id, "1912", "Broker"))!.Goods.SequenceEqual(
            upgradedSale.Goods
        ),
        "Upgraded demand persists without rerolling"
    );
    var loadedSales = await marketService.GetSalesAsync(id, "1910", "Broker");
    Check(
        loadedSales!.BuyerDm == -2 && loadedSales.Goods.All(g => g.Tons > 0),
        "Buyer DM and demand persist"
    );
    Check(
        loadedSales!.IsOutdated && loadedSales.Goods.SequenceEqual(savedSales.Goods),
        "Sale prices reload unchanged and are marked outdated"
    );
    Check(
        await marketService.GetSalesAsync(id, "1910", "Streetwise") is null
            && await marketService.GetSalesAsync(id, "1911", "Broker") is null,
        "Sales isolated by world and mode"
    );
    var savedMarket = await marketService.RollAsync(id, "1910", "Broker", 3, "Ht");
    Check(!savedMarket.IsOutdated, "Newly rolled market is current");
    var reloadedMarket = await marketService.GetAsync(id, "1910", "Broker");
    Check(reloadedMarket!.IsOutdated, "Market loaded from database is outdated");
    Check(
        reloadedMarket.SupplierDm == -2 && reloadedMarket.Goods.SequenceEqual(savedMarket.Goods),
        "Saved market restores supplier DM and generated goods"
    );
    Check(
        await marketService.GetAsync(id, "1911", "Broker") is null
            && await marketService.GetAsync(id, "1910", "Streetwise") is null,
        "Markets isolated by world and mode"
    );
    var passengerService = new PassengerMarketService(db);
    Check(
        await passengerService.GetAsync(id, "1910") is null,
        "No passenger roll before generation"
    );
    await passengerService.SaveAsync(id, "1910", routePassengers);
    await using (var reopenedRoute = new MtuDbContext(options))
    {
        var loadedRoute = await new PassengerMarketService(reopenedRoute).GetAsync(id, "1910");
        Check(
            loadedRoute is { DestinationSectorId: 42, DestinationHex: "0101", Distance: 3 }
                && loadedRoute.Destination == routePassengers.Destination
                && loadedRoute.Lines.SequenceEqual(routePassengers.Lines),
            "Passenger route survives reopening"
        );
    }
    var passengerRoll = PassengerTraffic.Roll("A788899-C", "A", 2, 1, () => 1);
    await passengerService.SaveAsync(id, "1910", passengerRoll);
    await using (var reopened = new MtuDbContext(options))
    {
        var loadedPassengers = await new PassengerMarketService(reopened).GetAsync(id, "1910");
        Check(
            loadedPassengers is not null
                && loadedPassengers.Effect == 2
                && loadedPassengers.Steward == 1
                && loadedPassengers.Lines.SequenceEqual(passengerRoll.Lines),
            "Passenger roll and inputs survive reopening"
        );
        Check(
            await new PassengerMarketService(reopened).GetAsync(id, "1911") is null,
            "Passenger rolls are isolated by world"
        );
    }
    passengerRoll = passengerRoll with { TargetDay = today };
    await passengerService.SaveAsync(id, "1910", passengerRoll);
    Check(
        (await passengerService.GetAsync(id, "1910"))!.TargetDay == today,
        "Passenger target date persists"
    );
    var selectedPassengerRoll = PassengerTraffic.Select(passengerRoll, "High", 1);
    await passengerService.SaveAsync(id, "1910", selectedPassengerRoll);
    await using (var reopened = new MtuDbContext(options))
    {
        var selection = await new PassengerMarketService(reopened).GetAsync(id, "1910");
        Check(
            selection!.Lines.Single(line => line.Passage == "High").Taken == 1,
            "Passenger selection persists"
        );
    }
    var replacementRoll = PassengerTraffic.Roll("A788899-C", "A", -1, 3, () => 2);
    await passengerService.SaveAsync(id, "1910", replacementRoll);
    Check(
        (await passengerService.GetAsync(id, "1910"))!.Lines.SequenceEqual(replacementRoll.Lines)
            && await db.TransportMarkets.CountAsync() == 1,
        "Reroll replaces the saved passenger market"
    );
    var freightService = new FreightMarketService(db);
    Check(
        await freightService.GetAsync(id, "1910") is null,
        "Freight is separate from passenger results"
    );
    await freightService.SaveAsync(id, "1910", routeFreight);
    await using (var reopenedRoute = new MtuDbContext(options))
    {
        var loadedRoute = await new FreightMarketService(reopenedRoute).GetAsync(id, "1910");
        Check(
            loadedRoute is { DestinationSectorId: 42, DestinationHex: "0101", Distance: 3 }
                && loadedRoute.Destination == routeFreight.Destination
                && loadedRoute.Lines.SequenceEqual(routeFreight.Lines),
            "Freight route survives reopening"
        );
    }
    await freightService.SaveAsync(id, "1910", freight);
    await using (var reopened = new MtuDbContext(options))
    {
        var restoredFreight = await new FreightMarketService(reopened).GetAsync(id, "1910");
        Check(
            restoredFreight is not null
                && restoredFreight.Effect == 1
                && restoredFreight.Lines.SequenceEqual(freight.Lines),
            "Freight results persist"
        );
        Check(
            await new FreightMarketService(reopened).GetAsync(id, "1911") is null,
            "Freight isolated by world"
        );
    }
    var oldFreight = freight with
    {
        Lines = freight
            .Lines.Select(line =>
                line.Type == "Minor" ? line with { LotSize = "1Dx5 (3 to 30)" } : line
            )
            .ToArray(),
    };
    await freightService.SaveAsync(id, "1910", oldFreight);
    var correctedFreight = await freightService.GetAsync(id, "1910");
    Check(
        correctedFreight!.Lines[1].LotSize == "1Dx5 (5 to 30)"
            && correctedFreight.Lines[1].Lots == oldFreight.Lines[1].Lots,
        "Legacy minor lot labels corrected without rerolling"
    );
    loadedCargo = loadedCargo with { TargetDay = today };
    await freightService.SaveAsync(id, "1910", loadedCargo);
    Check(
        (await freightService.GetAsync(id, "1910"))!.TargetDay == today,
        "Freight target date persists"
    );
    await freightService.SaveAsync(id, "1910", loadedCargo);
    await using (var reopened = new MtuDbContext(options))
    {
        var restoredCargo = await new FreightMarketService(reopened).GetAsync(id, "1910");
        Check(
            restoredCargo?.CargoCapacity == 16
                && restoredCargo.Lines.SequenceEqual(loadedCargo.Lines),
            "Cargo capacity and loaded tons persist"
        );
    }
    await freightService.SaveAsync(id, "1910", unarmedMail);
    Check(
        (await freightService.GetAsync(id, "1910"))!.Mail is { ArmedDm: 0, Effect: 4 },
        "Unarmed choice persists with mail roll"
    );
    await freightService.SaveAsync(id, "1910", acceptedMail);
    await using (var reopened = new MtuDbContext(options))
    {
        var savedMail = await new FreightMarketService(reopened).GetAsync(id, "1910");
        Check(
            savedMail?.Mail is { Rank: 2, SocDm: 1, Containers: 3, CargoTaken: 15 },
            "Mail result and cargo persist"
        );
    }
    await freightService.SaveAsync(id, "1910", sparseFreight);
    Check(
        (await freightService.GetAsync(id, "1910"))!.Lines.SequenceEqual(sparseFreight.Lines),
        "Freight reroll replaces saved result"
    );
    var oldSnapshot = reloadProvider.Catalogue;
    var mapService = reloadContainer.GetRequiredService<MapDataService>();
    Check(
        (await mapService.GetSectorAsync(id))!.Single().Name == "Original",
        "Initial cached map data"
    );
    var mapSector = (await mapService.GetOverviewAsync()).Sectors.Single();
    Check(
        mapSector.Borders.Single().Color == "lightblue"
            && mapSector.Routes.Single().EndOffsetX == 1,
        "Map overview exposes imported border and route geometry"
    );
    var notifications = 0;
    reloadProvider.Reloaded += () => notifications++;
    mock.Version = 2;
    await worker.RunAsync(progress);
    db.ChangeTracker.Clear();
    Check(
        (await db.Sectors.SingleAsync()).Id == id
            && (await db.Systems.SingleAsync()).Name == "Updated",
        "Existing sector fully refreshed with stable sector ID"
    );
    Check(
        (await db.Borders.SingleAsync()).Color == "red"
            && (await db.Routes.SingleAsync()).Color == "orange",
        "Explicit colours override stylesheet"
    );
    Check(
        (await marketService.GetAsync(id, "1910", "Broker"))!.Goods.SequenceEqual(
            savedMarket.Goods
        ),
        "Market survives sector system-row replacement"
    );
    Check(
        (await passengerService.GetAsync(id, "1910"))!.Lines.SequenceEqual(replacementRoll.Lines),
        "Passenger results survive system refresh"
    );
    await reloadProvider.ReloadAsync();
    Check(
        notifications == 1 && !ReferenceEquals(oldSnapshot, reloadProvider.Catalogue),
        "Reload publishes one new catalogue"
    );
    Check(
        oldSnapshot.Systems.Single().MainWorld.Name == "Original",
        "Readers retain an intact old snapshot"
    );
    Check(
        (await mapService.GetSectorAsync(id))!.Single().Name == "Updated",
        "Existing map service rebuilds after reload"
    );
    var refreshedSector = (await mapService.GetOverviewAsync()).Sectors.Single();
    Check(
        refreshedSector.Worlds.Single().Name == "Updated"
            && refreshedSector.Borders.Single().Color == "red"
            && refreshedSector.Routes.Single().Color == "orange",
        "Overview rebuilds world and geometry after reload"
    );
    Check(
        (await reloadContainer.GetRequiredService<ISearchService>().SearchAsync("Updated"))
            .Single()
            .SystemName == "Updated",
        "Search sees reloaded system"
    );
    Check(
        (
            await reloadContainer
                .GetRequiredService<WorldSelectionService>()
                .FindAsync(-4, -1, 19, 10)
        )!.Name == "Updated",
        "Selection sees reloaded system"
    );
    mock.FailMetadata = true;
    await worker.RunAsync(progress);
    db.ChangeTracker.Clear();
    Check(
        (await db.Systems.SingleAsync()).Name == "Updated" && reports.Last().Failed == 1,
        "Failed metadata fetch retains previous sector"
    );
    var validSnapshot = reloadProvider.Catalogue;
    await importConnection.CloseAsync();
    try
    {
        await reloadProvider.ReloadAsync();
        throw new Exception("Reload unexpectedly succeeded without database");
    }
    catch (Microsoft.Data.Sqlite.SqliteException) { }
    Check(
        ReferenceEquals(validSnapshot, reloadProvider.Catalogue) && notifications == 1,
        "Failed reload preserves the published snapshot and sends no notification"
    );
    Check(
        MetadataStyles.Color(
            "/* note */ border.Foo, border.Bar { color: blue; } border.Bar { color: red }",
            "border",
            "Bar"
        ) == "red",
        "Stylesheet grouped selectors and order"
    );
}
Console.WriteLine(
    "Metadata migration, full refresh, styles, cross-sector routes and failed-fetch preservation passed."
);

sealed class InlineProgress<T>(Action<T> action) : IProgress<T>
{
    public void Report(T value) => action(value);
}

sealed class RefreshClient(SectorSummary sector) : ITravellerMapClient
{
    public int Version = 1;
    public bool FailMetadata;
    public bool CancelAtStart,
        FailSectorList;

    public Task<IReadOnlyList<SectorSummary>> GetSectorsAsync(CancellationToken ct = default)
    {
        if (CancelAtStart)
            throw new OperationCanceledException();
        if (FailSectorList)
            throw new IOException("Sector list failed");
        return Task.FromResult<IReadOnlyList<SectorSummary>>([sector]);
    }

    public Task<SectorData> GetSectorDataAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(
            new SectorData
            {
                Systems =
                [
                    new StarSystem { Hex = "1910", Name = Version == 1 ? "Original" : "Updated" },
                ],
            }
        );

    public Task<SectorMetadata> GetMetadataAsync(
        string name,
        string milieu,
        CancellationToken ct = default
    )
    {
        if (FailMetadata)
            throw new HttpRequestException("test failure");
        return Task.FromResult(
            new SectorMetadata
            {
                Stylesheet = "border.SwCf { color: lightblue; } route.SwCf { color: #006600; }",
                Subsectors = [new MetadataSubsector { Index = "C", Name = "Regina" }],
                Allegiances =
                [
                    new MetadataAllegiance
                    {
                        Code = "SwCf",
                        Name = "Sword Worlds",
                        ShortName = "Sword Worlds",
                    },
                ],
                Borders =
                [
                    new MetadataBorder
                    {
                        Path = "0000 0100 0000",
                        Allegiance = "SwCf",
                        Color = Version == 2 ? "red" : null,
                    },
                ],
                Routes =
                [
                    new MetadataRoute
                    {
                        Start = "3209",
                        End = "0208",
                        EndOffsetX = 1,
                        Allegiance = "SwCf",
                        Color = Version == 2 ? "orange" : null,
                    },
                ],
            }
        );
    }
}

sealed class CatalogueCommandCounter
    : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
{
    public int Reads;

    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command,
        Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,
        CancellationToken cancellationToken = default
    )
    {
        Interlocked.Increment(ref Reads);
        return ValueTask.FromResult(result);
    }
}
