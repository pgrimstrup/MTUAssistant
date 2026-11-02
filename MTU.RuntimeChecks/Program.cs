using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;
using MTU.Core.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

Check(PassengerTraffic.DiceFor(1) == 0 && PassengerTraffic.DiceFor(2) == 1 && PassengerTraffic.DiceFor(6) == 2
    && PassengerTraffic.DiceFor(10) == 3 && PassengerTraffic.DiceFor(13) == 4 && PassengerTraffic.DiceFor(15) == 5
    && PassengerTraffic.DiceFor(16) == 6 && PassengerTraffic.DiceFor(19) == 9 && PassengerTraffic.DiceFor(20) == 10, "Passenger traffic table boundaries");
var passengers = PassengerTraffic.Roll("A788899-C", "A", 1, 2, () => 1);
Check(passengers.PopulationDm == 3 && passengers.StarportDm == 2 && passengers.ZoneDm == 1, "Passenger world modifiers");
Check(passengers.Lines.Count == 4 && passengers.Lines[0] is { Passage: "High", Roll: 2, ModifiedRoll: 7, Dice: 3, Passengers: 3 }
    && passengers.Lines[3] is { Passage: "Low", ModifiedRoll: 12, Dice: 4, Passengers: 4 }, "Passenger class modifiers and independent rolls");
var sparsePassengers = PassengerTraffic.Roll("X000100-0", "R", 0, 0, () => 1);
Check(sparsePassengers.PopulationDm == -4 && sparsePassengers.StarportDm == -3 && sparsePassengers.ZoneDm == -4
    && sparsePassengers.Lines.All(line => line.Passengers == 0), "Low population red zone passenger modifiers");
try { PassengerTraffic.Roll("???????-?", "", 0, 0); throw new Exception("Unknown UWP accepted"); }
catch (ArgumentException) { }

var selectedPassengers = PassengerTraffic.Select(passengers, "High", 2);
selectedPassengers = PassengerTraffic.Select(selectedPassengers, "Low", 3);
Check(PassengerTraffic.Fees[0].Payment(selectedPassengers) == 19100
    && PassengerTraffic.Fees[5].Payment(selectedPassengers) == 1228000, "Passenger payments use selected classes and distance");
try { PassengerTraffic.Select(passengers, "High", 4); throw new Exception("Too many passengers accepted"); }
catch (ArgumentException) { }
try { PassengerTraffic.Select(passengers, "Low", -1); throw new Exception("Negative passenger selection accepted"); }
catch (ArgumentException) { }
Check(FreightTraffic.DiceFor(1) == 0 && FreightTraffic.DiceFor(3) == 1 && FreightTraffic.DiceFor(5) == 2
    && FreightTraffic.DiceFor(8) == 3 && FreightTraffic.DiceFor(11) == 4 && FreightTraffic.DiceFor(14) == 5
    && FreightTraffic.DiceFor(16) == 6 && FreightTraffic.DiceFor(19) == 9 && FreightTraffic.DiceFor(20) == 10, "Freight traffic table boundaries");
var freight = FreightTraffic.Roll("A788899-C", "A", 1, () => 1);
Check(freight.PopulationDm == 4 && freight.StarportDm == 2 && freight.TechDm == 2 && freight.ZoneDm == -2, "Freight world modifiers");
Check(freight.Lines[0] is { Type: "Major", ModifiedRoll: 5, Dice: 2, Lots: 2 }
    && freight.Lines[1] is { Type: "Minor", ModifiedRoll: 9, Dice: 4, Lots: 4 }
    && freight.Lines[2] is { Type: "Incidental", ModifiedRoll: 11, Dice: 4, Lots: 4 }, "Freight type modifiers and independent rolls");
var loadingInput = freight with { Lines = freight.Lines.Select(line => line with {
    Lots = line.Type switch { "Major" => 3, "Minor" => 1, _ => 1 } }).Reverse().ToArray() };
var cargoDice = new Queue<int>([6, 2, 1, 2, 6]);
var loadedCargo = FreightTraffic.TakeCargo(loadingInput, 16, () => cargoDice.Dequeue());
Check(loadedCargo.CargoCapacity == 16 && loadedCargo.Lines.Single(l => l.Type == "Major").CargoTaken == 10
    && loadedCargo.Lines.Single(l => l.Type == "Minor").CargoTaken == 0
    && loadedCargo.Lines.Single(l => l.Type == "Incidental").CargoTaken == 6 && cargoDice.Count == 0,
    "Cargo loading prioritizes classes, skips oversized lots and fills remaining capacity");
var noCargo = FreightTraffic.TakeCargo(loadingInput, 0, () => throw new Exception("No dice at zero capacity"));
Check(noCargo.Lines.All(l => l.CargoTaken == 0), "Zero cargo capacity");
try { FreightTraffic.TakeCargo(loadingInput, -1); throw new Exception("Negative capacity accepted"); }
catch (ArgumentException) { }
Check(FreightTraffic.MailTrafficDm(-10) == -2 && FreightTraffic.MailTrafficDm(-9) == -1
    && FreightTraffic.MailTrafficDm(-4) == 0 && FreightTraffic.MailTrafficDm(4) == 0
    && FreightTraffic.MailTrafficDm(5) == 1 && FreightTraffic.MailTrafficDm(10) == 2, "Mail traffic modifier bands");
var mailDice = new Queue<int>([4, 4, 3]);
var withMail = FreightTraffic.RollMail(freight, 2, 1, () => mailDice.Dequeue());
Check(withMail.Mail is { FreightTrafficDm: 7, Effect: 4, TechDm: 0, Roll: 8, ModifiedRoll: 12, Containers: 3 }, "Mail succeeds at 12 and rolls containers");
var lowTechMail = FreightTraffic.RollMail(freight with { Tech = "5" }, 0, 0, () => 1);
Check(lowTechMail.Mail is { TechDm: -4, Containers: 0 }, "Mail failure and low tech penalty");
var cannotFit = FreightTraffic.TakeMail(withMail, 14);
Check(cannotFit.Mail is { CargoTaken: 0, InsufficientSpace: true }, "Mail cannot exceed capacity");
var acceptedMail = FreightTraffic.TakeMail(withMail, 20);
Check(acceptedMail.Mail is { CargoTaken: 15, InsufficientSpace: false }, "Mail accepted when it fits");
var afterMailCargo = FreightTraffic.TakeCargo(acceptedMail, 20, () => 1);
Check(afterMailCargo.Lines.Sum(l => l.CargoTaken ?? 0) <= 5 && afterMailCargo.Mail?.CargoTaken == 15, "Freight respects accepted mail capacity");
try { FreightTraffic.TakeCargo(acceptedMail, 14); throw new Exception("Cargo capacity below accepted mail allowed"); }
catch (ArgumentException) { }
var sparseFreight = FreightTraffic.Roll("X000100-0", "R", 0, () => 1);
Check(sparseFreight.PopulationDm == -4 && sparseFreight.StarportDm == -3 && sparseFreight.TechDm == -1
    && sparseFreight.ZoneDm == -6 && sparseFreight.Lines.All(line => line.Lots == 0), "Sparse red zone freight modifiers");
try { FreightTraffic.Roll("A788899-?", "", 0); throw new Exception("Unknown freight tech level accepted"); }
catch (ArgumentException) { }

var dateCampaign = new Campaign(1, "Date check", 2, 1105, null, null, true);
var today = CampaignDates.Today(dateCampaign);
Check(CampaignDates.Status(today, dateCampaign).Text == "Current" && CampaignDates.Status(today + 1, dateCampaign).Text == "Current", "Today and future are current");
Check(CampaignDates.Status(today - 1, dateCampaign).Text == "Outdated 1 days" && CampaignDates.Status(today - 6, dateCampaign).Style == "outdated", "Recent results outdated");
Check(CampaignDates.Status(today - 7, dateCampaign) == ("Expired 7 days", "expired"), "Seven days expires across year boundary");
Check(CampaignDates.Status(null, dateCampaign).Text == "Date unknown" && CampaignDates.Status(today, null).Text == "Select campaign", "Undated legacy and missing campaign status");
Check(CampaignDates.RemoteTarget(today, 3, () => 1) == today - 7 && CampaignDates.RemoteTarget(today, 4, () => 6) == today - 19, "Remote destination delay and distance boundary");
Check(CampaignDates.Status(today - 7, dateCampaign with { Day = 1, Year = 1105 }).Text == "Outdated 6 days", "Date status updates when campaign date changes");

Check(PoliticalColor.Resolve(null, "ImDd", false) == "red", "Imperial border fallback");
Check(PoliticalColor.Resolve("", "Im", true) == "limegreen", "Imperial route fallback");
Check(PoliticalColor.Resolve(null, "ZhIN", false) == "blue", "Zhodani border fallback");
Check(PoliticalColor.Resolve(null, "ZhCo", true) == "lightblue", "Zhodani route fallback");
Check(PoliticalColor.Resolve("orange", "ImDd", false) == "orange", "Stored colours take precedence");
Check(PoliticalColor.Resolve(null, "DaCf", true) is null && PoliticalColor.Resolve(null, null, true) is null, "Other allegiances retain existing fallback");
Check(PoliticalColor.Resolve(null, "AsMw", false) == "yellow", "Aslan border fallback");
Check(PoliticalColor.Resolve("purple", "AsMw", false) == "purple", "Aslan stored border colour takes precedence");
Check(PoliticalColor.Resolve(null, "AsMw", true) is null, "Aslan route colour unchanged");
var profile = UwpLookup.Describe("a788899-c");
Check(profile.Count == 8 && profile[0].Code == 'A' && profile[7].Code == 'C', "UWP positions or case handling");
Check(profile.All(a => !a.Description.Contains("Unrecognised")), "Known profile descriptions");
foreach (var value in new string?[] { null, "", "A78", "A788899?C", "A788899-CC", "?????????" })
    Check(UwpLookup.Describe(value).All(a => a.Description == "Unknown"), "Malformed profile handling");
Check(UwpLookup.Describe("A78?899-C")[3].Description == "Unknown", "Unknown attribute handling");
Check(UwpLookup.Describe("!788899-C")[0].Description.Contains("Unrecognised"), "Unsupported code handling");
Check(UwpLookup.Describe("X000000-0")[1].Description == "Asteroid belt", "Asteroid size");
Check(UwpLookup.Describe("X000000-0")[4].Description.Contains("possibly uninhabited"), "Population zero");
var populationDescription = UwpLookup.Describe("A788899-C", "703")[4].Description;
Check(populationDescription.StartsWith(profile[4].Description)
    && populationDescription.Contains($"{700_000_000:N0} inhabitants (multiplier 7)"), "Population multiplier and existing description");
Check(UwpLookup.Describe("X000000-0", "000")[4].Description.Contains("Population: 0 inhabitants"), "Uninhabited population");
Check(UwpLookup.Describe("X000000-0", "503")[4].Description.Contains("Population: 5 inhabitants"), "Exponent zero with nonzero multiplier");
Check(UwpLookup.Describe("A788F99-C", "903")[4].Description.Contains($"{9_000_000_000_000_000L:N0} inhabitants"), "Large population");
foreach (var pbg in new string?[] { null, "", "?03", "A03" })
    Check(UwpLookup.Describe("A788899-C", pbg)[4].Description == profile[4].Description, "Unavailable multiplier");
Check(UwpLookup.Describe("A788?99-C", "703")[4].Description == "Unknown", "Unknown population exponent");

var landDescription = UwpLookup.Describe("A868899-C")[3].Description;
Check(UwpLookup.Describe("A868899-C")[1].Description == $"Approximately {12_800:N0} km diameter. Approximate surface gravity: 1 g (assuming Earth-like density)", "Earth-sized world gravity and retained diameter");
Check(UwpLookup.Describe("A168899-C")[1].Description.Contains($"{0.122:0.###} g"), "Small world gravity");
Check(UwpLookup.Describe("AF68899-C")[1].Description.Contains($"{1.875:0.###} g"), "Largest standard size gravity");
foreach (var uwp in new[] { "A068899-C", "AS68899-C", "A?68899-C", "AG68899-C", "invalid" })
    Check(!UwpLookup.Describe(uwp)[1].Description.Contains("surface gravity"), "No gravity estimate without a standard size");
Check(landDescription.StartsWith("Approximately 80% surface liquid coverage")
    && landDescription.Contains($"{77_207_781:N0}–{128_679_635:N0} km² (75–85% liquid coverage)"), "Spherical surface and land area range");
Check(UwpLookup.Describe("A860899-C")[3].Description.Contains($"{488_982_613:N0}–{514_718_540:N0} km² (0–5% liquid coverage)"), "Dry world lower clamp");
Check(UwpLookup.Describe("A86A899-C")[3].Description.Contains($"{0:N0}–{25_735_927:N0} km² (95–100% liquid coverage)"), "Water world upper clamp");
foreach (var uwp in new[] { "A068899-C", "AS68899-C", "A?68899-C", "AG68899-C", "A86?899-C", "A86B899-C", "invalid" })
    Check(!UwpLookup.Describe(uwp)[3].Description.Contains("land area"), "Unavailable size or hydrographics");

var tradeCodes = TradeCodeLookup.Describe("Ht Ri Pa Ph An Cp (Amindii)2 O:0101 { Hi notes } [Human]");
Check(tradeCodes.Select(code => code.Code).SequenceEqual(new[] { "Ht", "Ri", "Pa", "Ph", "An", "Cp" }), "Trade codes from stored remarks, excluding other annotations");
Check(tradeCodes[1].Name == "Rich" && tradeCodes.All(code => code.Description.Length > 0), "Central trade descriptions");
Check(TradeCodeLookup.Describe("Ag ag AG").Count == 1, "Duplicate trade codes");
Check(TradeCodeLookup.Describe("RsA")[0].Code == "RsA", "Designated research station");
Check(TradeCodeLookup.Describe("Zz")[0].Name == "Unrecognised trade code", "Custom code fallback");
Check(TradeCodeLookup.Describe(null).Count == 0 && TradeCodeLookup.Describe("- (Minor race) [Human]").Count == 0, "Empty or non-trade remarks");

SelectedWorld SystemWorld(string uwp = "A788899-C", string pbg = "713", string w = "8",
    string stellar = "F7 V BD M3 V", string remarks = "") =>
    new("Test", "Test sector", "0101", uwp, pbg, w, stellar, remarks, "", null);

var systemData = SystemDataLookup.Describe(SystemWorld());
Check(systemData.WorldDescription == "9 worlds, including 3 gas giants and 1 asteroid belt.", "Combined world description");
Check(SystemDataLookup.Describe(SystemWorld(pbg: "703")).WorldDescription == "9 worlds, including 3 gas giants.", "Omit absent asteroid belts");
Check(SystemDataLookup.Describe(SystemWorld(pbg: "700", w: "0")).WorldDescription == "1 world.", "Singular world and no empty inclusions");
Check(SystemDataLookup.Describe(SystemWorld(pbg: "701", remarks: "Sa")).WorldDescription == "8 worlds, including 1 gas giant. The main world is a satellite.", "Satellite description");
Check(SystemDataLookup.Describe(SystemWorld(uwp: "X000000-0", pbg: "700")).WorldDescription == "8 worlds, including 1 asteroid belt. The main world is an asteroid belt.", "Main-world belt description");
Check(SystemDataLookup.Describe(SystemWorld(pbg: "???", w: "?")).WorldDescription == "Unknown number of worlds.", "Unknown counts remain unknown");
Check(systemData.Stars.Select(s => s.Designation).SequenceEqual(new[] { "A", "B", "C" }), "Stellar letter designations in source order");
Check(systemData is { Worlds: 9, GasGiants: 3, AsteroidBelts: 1, StellarDataKnown: true }
    && systemData.Stars.Count == 3, "System totals and multi-star classification");
Check(systemData.Stars[0] is { Code: "F7 V", Description: "Yellow-white main-sequence star" }
    && systemData.Stars[1].Description == "Brown dwarf"
    && systemData.Stars[2].Description == "Red main-sequence star", "Star types");
Check(SystemDataLookup.Describe(SystemWorld(uwp: "X000000-0")) is { Worlds: 8, AsteroidBelts: 2 }, "Main-world asteroid belt adjustments");
Check(SystemDataLookup.Describe(SystemWorld(remarks: "Hi Sa Ht")).Worlds == 8, "Main-world moon excluded from extra worlds");
Check(SystemDataLookup.Describe(SystemWorld(remarks: "(Saurian) O:0101")).Worlds == 9, "Moon code must be a complete remark");
Check(SystemDataLookup.Describe(SystemWorld(pbg: "7AB", w: "12")) is { Worlds: 13, GasGiants: 11, AsteroidBelts: 10 }, "Decimal W and extended-hex PBG counts");
Check(SystemDataLookup.Describe(SystemWorld(pbg: "700", w: "0")) is { Worlds: 1, GasGiants: 0, AsteroidBelts: 0 }, "Known zero counts");
Check(SystemDataLookup.Describe(SystemWorld(pbg: "7?3", w: "?")) is { Worlds: null, GasGiants: 3, AsteroidBelts: null }, "Independent unknown counts");
Check(SystemDataLookup.Describe(SystemWorld(uwp: "A?88899-C")) is { Worlds: null, AsteroidBelts: null }, "Unknown main-world classification");
foreach (var value in new[] { "", "-", "?", "F7 V unknown", "F7 V ?", "garbage" })
    Check(!SystemDataLookup.Describe(SystemWorld(stellar: value)).StellarDataKnown, "Incomplete stellar data must not imply a known count");
var compactStars = SystemDataLookup.Describe(SystemWorld(stellar: "G2V M1 VI K3 III D DA BH NS PSR"));
Check(compactStars.StellarDataKnown && compactStars.Stars.Count == 8
    && compactStars.Stars[1].Description == "Red subdwarf"
    && compactStars.Stars[2].Description == "Orange giant"
    && compactStars.Stars[3].Description == "White dwarf"
    && compactStars.Stars[5].Description == "Black hole", "Stellar luminosity classes and remnants");

using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var services = new ServiceCollection();
var commands = new CatalogueCommandCounter();
services.AddDbContext<MtuDbContext>(options => options.UseSqlite(connection).AddInterceptors(commands));
services.AddMtuCatalogue();
using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
using (var scope = provider.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
    await db.Database.EnsureCreatedAsync();
    db.Sectors.AddRange(
        new SectorEntity { Name = "Spinward Marches", X = -4, Y = -1, Milieu = "M1105",
            Subsectors = [new SubsectorEntity { Letter = "C", Name = "Regina" }],
            Allegiances = [new AllegianceEntity { Code = "ImDd", Name = "Third Imperium, Domain of Deneb" }],
            Systems = [
                new SystemEntity { Name = "Regina", Hex = "1910", Uwp = "A788899-C", Pbg = "703", W = "8", Stellar = "F7 V BD M3 V", Remarks = "Sa", A = "ImDd" },
                new SystemEntity { Name = "Unmapped polity", Hex = "1911", A = "Custom" },
                new SystemEntity { Name = "No allegiance", Hex = "1912" }
            ] },
        new SectorEntity { Name = "Different era", X = -4, Y = -1, Milieu = "M1120",
            Allegiances = [new AllegianceEntity { Code = "ImDd", Name = "Different era polity" }],
            Systems = [new SystemEntity { Name = "Other Regina", Hex = "1910", Uwp = "X000000-0", A = "ImDd" }] });
    await db.SaveChangesAsync();
}
commands.Reads = 0;
var catalogueProvider = provider.GetRequiredService<SystemDataProvider>();
await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => catalogueProvider.InitializeAsync()));
Check(commands.Reads == 6, "Concurrent initialization reads each catalogue table exactly once");
var snapshot = catalogueProvider.Catalogue;

await catalogueProvider.InitializeAsync();
Check(ReferenceEquals(snapshot, catalogueProvider.Catalogue) && commands.Reads == 6, "Initialization reuses the published snapshot");
Check(snapshot.Systems.Length == 4 && snapshot.Systems.All(s => s.Bodies.IsEmpty), "All milieus loaded with no invented system bodies");
var spinward = snapshot.Sectors.Single(s => s.Milieu == "M1105");
var adjacent = new CatalogueSector(-1,"Adjacent",spinward.X+1,spinward.Y,"M1105");
Check(TradeDestinations.Distance(spinward,"3201",adjacent,"0101") == 1, "Distance across sector seam");
Check(TradeDestinations.Distance(adjacent,"0101",spinward,"3201") == 1, "Distance symmetric across negative coordinates");
Check(TradeDestinations.Distance(spinward,"1910",spinward,"1916") == 6 && TradeDestinations.Distance(spinward,"1910",spinward,"1917") == 7, "Six parsec distance boundary");
Check(TradeDestinations.Distance(spinward,"0000",spinward,"1910") is null, "Invalid hex does not produce a destination");
var nearby = TradeDestinations.Within(snapshot,spinward.Id,"1910");
Check(nearby.Select(d => d.System.MainWorld.Name).SequenceEqual(new[] {"No allegiance", "Unmapped polity"}), "Nearby worlds sorted alphabetically with current world and other milieu excluded");
Check(nearby.All(d => d.Distance <= 6), "Only nearby destinations included");
commands.Reads = 0;
// Destroy the in-memory database: every consumer must now work solely from the snapshot.
await connection.CloseAsync();
using (var scope = provider.CreateScope())
{
    Check(ReferenceEquals(catalogueProvider, scope.ServiceProvider.GetRequiredService<SystemDataProvider>()), "Catalogue shared across scopes");
    var selection = scope.ServiceProvider.GetRequiredService<WorldSelectionService>();
    var world = await selection.FindAsync(-4, -1, 19, 10);
    Check(world is { Name: "Regina", Sector: "Spinward Marches", Hex: "1910", Uwp: "A788899-C", Pbg: "703", W: "8", Stellar: "F7 V BD M3 V", Remarks: "Sa" }, "Database selection, system data and milieu");
    Check(world is { AllegianceCode: "ImDd", AllegianceName: "Third Imperium, Domain of Deneb" }, "Sector-specific allegiance polity lookup");
    Check(await selection.FindAsync(-4, -1, 19, 11) is { AllegianceCode: "Custom", AllegianceName: null }, "Unmapped allegiance retains the world and code");
    Check(await selection.FindAsync(-4, -1, 19, 12) is { AllegianceCode: "", AllegianceName: null }, "Missing allegiance retains the world");
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
    Check(overview.Sectors.Count == 1 && overview.Sectors[0].Name == "Spinward Marches", "Database map overview filters milieu");
    Check(overview.Sectors[0].Hexes.Count == 3 && overview.MinSectorX == TileRefreshWorker.MinX,
        "Overview points and atlas origin without tile files");
    Check(overview.Sectors[0].Worlds.Single(w => w.Hex == "1910") is
        { Name: "Regina", Uwp: "A788899-C", Remarks: "Sa" }, "Overview includes world label classifications before sector details load");
    var details = await service.GetSectorAsync(overview.Sectors[0].Id);
    Check(details is not null && details.Single(w => w.Hex == "1910") is
        { Name: "Regina", Uwp: "A788899-C", Pbg: "703", Remarks: "Sa", Allegiance: "ImDd" }, "Sector detail fields");
    Check(await service.GetSectorAsync(-1) is null, "Missing sector is not an empty valid sector");
    var otherEra = snapshot.Sectors.Single(s => s.Milieu == "M1120");
    Check(await service.GetSectorAsync(otherEra.Id) is null, "Sector detail filters milieu");
}
using (var emptyConnection = new SqliteConnection("Data Source=:memory:"))
{
    await emptyConnection.OpenAsync();
    using var emptyDb = new MtuDbContext(new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(emptyConnection).Options);
    await emptyDb.Database.EnsureCreatedAsync();
    var emptyServices = new ServiceCollection();
    emptyServices.AddDbContext<MtuDbContext>(options => options.UseSqlite(emptyConnection));
    emptyServices.AddMtuCatalogue();
    using var emptyProvider = emptyServices.BuildServiceProvider();
    await emptyProvider.GetRequiredService<SystemDataProvider>().InitializeAsync();
    var overview = await emptyProvider.GetRequiredService<MapDataService>().GetOverviewAsync();
    Check(overview.Sectors.Count == 0 && overview.Columns > 0 && overview.Rows > 0, "Empty database has usable atlas bounds");
}
Console.WriteLine("Database-driven map overview, sector details and empty database checks passed.");

var search = provider.GetRequiredService<ISearchService>();
var results = await search.SearchAsync("  REGina  ");
Check(results.Count == 3 && results[0].SubsectorName == "Regina", "Search trimming, case insensitivity, subsector and system results");
var regina = results.Single(r => r.SystemName == "Regina");
Check(regina is { SectorName: "Spinward Marches", SubsectorLetter: "C", SubsectorName: "Regina", SubsectorHex: "0310", SectorHex: "1910" }, "Search subsector context preserved");
Check(results.Any(r => r.SystemName == "Other Regina"), "Search retains its existing all-milieu scope");
regina.SystemName = "Changed UI result";
Check((await search.SearchAsync("Regina")).Any(r => r.SystemName == "Regina"), "UI results cannot mutate the catalogue");
Check((await search.SearchAsync("re")).Count == 0, "Minimum search term length");
Check((await search.SearchAsync("Spinward")).Single().SectorName == "Spinward Marches", "Sector search");
await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ => {
    Check((await search.SearchAsync("Regina")).Count == 3, "Concurrent search snapshot");
    Check((await provider.GetRequiredService<MapDataService>().GetOverviewAsync()).Sectors.Count == 1, "Concurrent map snapshot");
}));
using (var cancellation = new CancellationTokenSource()) {
    cancellation.Cancel();
    try { await search.SearchAsync("Regina", cancellation.Token); throw new Exception("Search ignored cancellation"); }
    catch (OperationCanceledException) { }
    try { await provider.GetRequiredService<MapDataService>().GetOverviewAsync(cancellation.Token); throw new Exception("Map ignored cancellation"); }
    catch (OperationCanceledException) { }
}
Check(commands.Reads == 0, "Search, map and selection perform no SQL after initialization, even with database unavailable");
Console.WriteLine("Shared startup catalogue, concurrent initialization, search, snapshot isolation and zero request-time SQL checks passed.");

// Purchase price uses 3D, one strongest matching modifier per column and the skill DM.
Check(TradePricing.Percentage(-20) == 200 && TradePricing.Percentage(0) == 175
    && TradePricing.Percentage(8) == 100 && TradePricing.Percentage(23) == 25 && TradePricing.Percentage(40) == 25, "Purchase price table bounds");
var priceExample = TradePricing.Price(new MarketLine(11, "Example", 1, 10, false, false), 2, "In Ht Ni Po", DiceForPrice(2,3,4));
Check(priceExample is { PriceRoll:9, PurchaseDm:3, SaleDm:2, PricePercent:80, BasePrice:20000, BuyPrice:16000 }, "Highest matching DMs and price per ton");
Check(TradePricing.FormatTradeModifiers(priceExample, false) == "+3/-2", "Buying displays individual trade DMs");
Check(TradePricing.FormatTradeModifiers(priceExample, true) == "+2/-3", "Selling displays individual trade DMs");
Check(TradePricing.FormatTradeModifiers(priceExample with { PurchaseDm = 0, SaleDm = 0 }, false) == "", "Zero trade DMs are blank");
Check(TradePricing.FormatTradeModifiers(priceExample with { PurchaseDm = 1, SaleDm = 1 }, false) == "+1/-1", "Offsetting trade DMs remain visible");
Check(TradePricing.FormatTradeModifiers(priceExample with { PurchaseDm = 0, SaleDm = -2 }, true) == "-2", "Negative sale DMs retain their sign");
Check(TradePricing.FormatTradeModifiers(priceExample with { PurchaseDm = null, SaleDm = null }, true) == "", "Missing trade DMs are blank");
var supplierExample = TradePricing.Price(new MarketLine(11, "Example", 1, 10, false, false), 2, "In Ht Ni Po", DiceForPrice(2,3,4), -2);
Check(supplierExample is { PricePercent:90, BuyPrice:18000 }, "Signed supplier DM is added to the purchase roll");
var positiveSupplier = TradePricing.Price(new MarketLine(11, "Example", 1, 10, false, false), 2, "In Ht Ni Po", DiceForPrice(2,3,4), 2);
Check(positiveSupplier is { PricePercent:70, BuyPrice:14000 }, "Positive supplier DM is added rather than subtracted");
Check(TradePricing.Price(new MarketLine(66, "Exotics", 1, null, false, false), 0, "", DiceForPrice()).BuyPrice is null, "Exotics remain referee-priced");
static Func<int> DiceForPrice(params int[] values) { var queue = new Queue<int>(values); return () => queue.Dequeue(); }

Check(TradePricing.SalePercentage(-99) == 30 && TradePricing.SalePercentage(10) == 90
    && TradePricing.SalePercentage(11) == 100 && TradePricing.SalePercentage(23) == 160
    && TradePricing.SalePercentage(99) == 160, "Sale price table boundaries");
var saleExample = TradePricing.SalePrice(new MarketLine(11, "Example", 0, null, false, false), 2, "In Ht Ni Po", DiceForPrice(2,3,4));
Check(saleExample is { PriceRoll:9, PurchaseDm:3, SaleDm:2, PricePercent:90, BasePrice:20000, SalePrice:18000 }, "Sale price adds skill and highest sale DM, subtracts highest purchase DM");
var buyerExample = TradePricing.SalePrice(new MarketLine(11, "Example", 0, null, false, false), 2, "In Ht Ni Po", DiceForPrice(2,3,4), -2);
Check(buyerExample is { PricePercent:80, SalePrice:16000 }, "Signed buyer DM is added to the sale roll");
var positiveBuyer = TradePricing.SalePrice(new MarketLine(11, "Example", 0, null, false, false), 2, "In Ht Ni Po", DiceForPrice(2,3,4), 2);
Check(positiveBuyer is { PricePercent:105, SalePrice:21000 }, "Positive buyer DM is added to the sale roll");
// Exact Imports membership for Regina's trade codes: commons plus matching positive Sale DMs.
var reginaImports = MarketGenerator.GenerateSales("Broker", 0, "Ri Pa Ph An Cp Sa Ht", () => 3);
int[] reginaExpected = [11,12,13,14,15,16,21,23,25,31,32,34,35,36,42,43,44,45,46,51,54,55];
Check(reginaImports.Goods.Select(g => g.D66).SequenceEqual(reginaExpected), "Regina Imports match exact common and positive sale demand");
Check(!TradePricing.HasSaleDemand(26, "An") && TradePricing.HasSaleDemand(26, "In"), "Demand matches complete codes, not substrings");
Check(!TradePricing.HasSaleDemand(45, "Ag") && TradePricing.HasSaleDemand(45, "Ag Ht"), "Negative sale DM alone does not create demand");
var noCodes = MarketGenerator.GenerateSales("Broker", 0, "", () => 3);
Check(noCodes.Goods.Select(g => g.D66).SequenceEqual(new[] {11,12,13,14,15,16}), "No trade codes produces only common goods");
var saleBroker = MarketGenerator.GenerateSales("Broker", 2, "In", () => 3);
var saleStreet = MarketGenerator.GenerateSales("Streetwise", 2, "In", () => 3);
Check(saleBroker.BuyerDm == -2 && saleBroker.DemandVersion == 1 && saleBroker.Goods.Single(g => g.D66 == 11).Tons == 60, "Demand uses commodity tonnage dice and records default buyer DM");
Check(saleBroker.Goods.Count(g => g.D66 <= 16) == 6 && saleBroker.Goods.Any(g => g.D66 == 61) == false, "Broker sale demand includes common and excludes illegal goods");
Check(saleStreet.Goods.Any(g => g.D66 == 61 && g.Illegal) && saleStreet.Goods.All(g => g.D66 > 16), "Streetwise includes illegal matching demand but excludes common goods");
Check(!saleStreet.Goods.Any(g => g.D66 == 62), "Imports exclude nonmatching sale demand");
// Fixed dice streams cover random duplicates, illegal rerolls and stolen goods.
static Func<int> Dice(params int[] values) { var queue = new Queue<int>(values); return () => queue.Count > 0 ? queue.Dequeue() : 1; }
var brokerDice = Enumerable.Repeat(1, 12).Concat(new[] { 1, 6, 1, 1, 1, 1, 1 }).ToArray();
var brokerMarket = MarketGenerator.Generate("Broker", 2, "", Dice(brokerDice));
Check(brokerMarket.Goods.Count == 6 && brokerMarket.Goods.All(g => !g.Illegal && !g.Stolen), "Broker commons and illegal reroll");
Check(brokerMarket.Goods.Single(g => g.D66 == 11) is { Lots: 2, Tons: 40 }, "Duplicate random good adds tonnage");
var streetMarket = MarketGenerator.Generate("Streetwise", -1, "Ht", Dice(1, 1, 1, 1, 1, 1, 1));
Check(streetMarket.Goods.Count == 3 && streetMarket.Goods.Count(g => g.Illegal) == 2
    && streetMarket.Goods.Single(g => g.D66 == 11).Stolen, "Streetwise illegal availability and stolen random goods");
Check(TradeGoods.All.Count == 36 && TradeGoods.All.Select(g => g.D66).Distinct().Count() == 36, "D66 commodity coverage");

// Upgrade an existing database, then prove repeated refresh replaces metadata atomically.
using (var importConnection = new SqliteConnection("Data Source=:memory:"))
{
    await importConnection.OpenAsync();
    var options = new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(importConnection).Options;
    await using var db = new MtuDbContext(options);
    var migrator = db.GetService<IMigrator>();
    await migrator.MigrateAsync("20261020100154_InitialCreate");
    await migrator.MigrateAsync();
    var summary = new SectorSummary { X = -4, Y = -1, Milieu = "M1105", Names = [new SectorName { Text = "Spinward Marches" }] };
    var mock = new RefreshClient(summary);
    var reports = new List<TileRefreshProgress>();
    var worker = new TileRefreshWorker(mock, new SectorImporter(db));
    var progress = new InlineProgress<TileRefreshProgress>(reports.Add);
    await worker.RunAsync(progress);
    var sector = await db.Sectors.SingleAsync();
    var id = sector.Id;
    Check((await db.Borders.SingleAsync()).Color == "lightblue", "Stylesheet border colour");
    var route = await db.Routes.SingleAsync();
    Check(route.Color == "#006600" && route.EndOffsetX == 1 && route.End == "0208", "Route style and cross-sector offset");
    Check((await db.Allegiances.SingleAsync()).ShortName == "Sword Worlds", "Metadata allegiance fields");
    Check((await db.Subsectors.SingleAsync()).Name == "Regina", "Metadata subsector name");
    Check(reports.Last() is { Total: 1, Completed: 1, Failed: 0 }, "Progress counts sectors, not images");
    db.ChangeTracker.Clear();
    var reloadServices = new ServiceCollection();
    reloadServices.AddDbContext<MtuDbContext>(o => o.UseSqlite(importConnection));
    reloadServices.AddMtuCatalogue();
    using var reloadContainer = reloadServices.BuildServiceProvider();
    var reloadProvider = reloadContainer.GetRequiredService<SystemDataProvider>();
    await reloadProvider.InitializeAsync();
    var campaignService = new CampaignService(db);
    await campaignService.SaveAsync(new Campaign(0, "First Campaign", 1, 1105, id, "1910", false));
    var firstCampaign = (await campaignService.ListAsync()).Single();
    Check(firstCampaign.IsActive && firstCampaign.LocationSectorId == id && firstCampaign.LocationHex == "1910", "New campaign is active with persistent location");
    await campaignService.AdjustDateAsync(firstCampaign.Id, -1);
    Check((await campaignService.ListAsync()).Single() is { Day: 365, Year: 1104 }, "Subtract one day crosses year");
    await campaignService.AdjustDateAsync(firstCampaign.Id, 7);
    Check((await campaignService.ListAsync()).Single() is { Day: 7, Year: 1105 }, "Add seven days crosses year");
    await campaignService.AdjustDateAsync(firstCampaign.Id, -7);
    await campaignService.AdjustDateAsync(firstCampaign.Id, 1);
    await using (var dateReopened = new MtuDbContext(options))
        Check((await new CampaignService(dateReopened).ListAsync()).Single() is { Day: 1, Year: 1105 }, "Date adjustment persists across contexts");
    await campaignService.SaveAsync(firstCampaign with { Day = 1, Year = 1 });
    try { await campaignService.AdjustDateAsync(firstCampaign.Id, -1); throw new Exception("Date before year one accepted"); }
    catch (ArgumentException) { }
    await campaignService.SaveAsync(firstCampaign);
    await campaignService.SaveAsync(new Campaign(0, "Second Campaign", 365, 1106, null, null, false));
    var campaignList = await campaignService.ListAsync();
    Check(campaignList.Count == 2 && campaignList.Count(c => c.IsActive) == 1 && campaignList.Single(c => c.IsActive).Name == "Second Campaign", "Creating campaign switches active campaign");
    await campaignService.ActivateAsync(firstCampaign.Id);
    await using (var reopened = new MtuDbContext(options))
    {
        var persisted = (await new CampaignService(reopened).ListAsync()).Single(c => c.IsActive);
        Check(persisted.Id == firstCampaign.Id && persisted.Day == 1 && persisted.Year == 1105 && persisted.LocationHex == "1910", "Active campaign survives reopening");
    }
    await campaignService.SaveAsync(firstCampaign with { Name = "Updated Campaign", Day = 100 });
    Check((await campaignService.ListAsync()).Single(c => c.Id == firstCampaign.Id) is { Name: "Updated Campaign", Day: 100, IsActive: true }, "Campaign editing preserves active status");
    try { await campaignService.SaveAsync(firstCampaign with { Day = 366 }); throw new Exception("Invalid day accepted"); }
    catch (ArgumentException) { }
    await campaignService.DeleteAsync(firstCampaign.Id);
    Check((await campaignService.ListAsync()).Single() is { Name: "Second Campaign", IsActive: true }, "Deleting active campaign selects another");
    await campaignService.DeleteAsync((await campaignService.ListAsync()).Single().Id);
    Check((await campaignService.ListAsync()).Count == 0, "Deleting last campaign leaves no active campaign");
    var marketService = new TradingService(db);
    db.TradeMarkets.Add(new TradeMarketEntity { SectorId = id, Hex = "0101", Mode = "Broker", ResultJson = "{\"Mode\":\"Broker\",\"SkillDm\":2,\"RolledUtc\":\"2026-01-01T00:00:00Z\",\"RandomRolls\":1,\"Goods\":[{\"D66\":11,\"Name\":\"Common Electronics\",\"Lots\":2,\"Tons\":40,\"Illegal\":false,\"Stolen\":false}]}" });
    await db.SaveChangesAsync();
    var legacy = await marketService.GetAsync(id, "0101", "Broker", "In Ht");
    Check(legacy is { PricingVersion:1 } && legacy.Goods.Single() is { Lots:2, Tons:40, BasePrice:20000, PricePercent:not null, BuyPrice:not null }, "Legacy market priced without rerolling goods");
    Check((await marketService.GetAsync(id, "0101", "Broker", "In Ht"))!.Goods.SequenceEqual(legacy!.Goods), "Legacy prices are persisted and not rerolled");
    var datedMarket = await marketService.RollAsync(id, "1913", "Broker", 0, "", targetDay: today);
    Check((await marketService.GetAsync(id, "1913", "Broker"))!.TargetDay == today, "Buying target date persists");
    var datedSales = await marketService.RollSalesAsync(id, "1914", "Broker", 0, "", targetDay: today - 14);
    Check((await marketService.GetSalesAsync(id, "1914", "Broker"))!.TargetDay == today - 14, "Selling target date persists");
    var savedSales = await marketService.RollSalesAsync(id, "1910", "Broker", 3, "In");
    var legacySale = savedSales with { DemandVersion = 0, BuyerDm = null, Goods = savedSales.Goods.Select(g => g with { Tons = null }).ToArray() };
    db.TradeMarkets.Add(new TradeMarketEntity { SectorId = id, Hex = "1912", Mode = "Sale:Broker", ResultJson = System.Text.Json.JsonSerializer.Serialize(legacySale) });
    await db.SaveChangesAsync();
    var upgradedSale = await marketService.GetSalesAsync(id, "1912", "Broker");
    Check(upgradedSale is { IsOutdated:true, DemandVersion:1, BuyerDm:null } && upgradedSale.Goods.All(g => g.Tons > 0), "Older sale market gains demand without inventing a buyer DM");
    Check(upgradedSale!.Goods.Select(g => g.SalePrice).SequenceEqual(legacySale.Goods.Select(g => g.SalePrice)), "Demand upgrade preserves saved prices");
    Check((await marketService.GetSalesAsync(id, "1912", "Broker"))!.Goods.SequenceEqual(upgradedSale.Goods), "Upgraded demand persists without rerolling");
    var loadedSales = await marketService.GetSalesAsync(id, "1910", "Broker");
    Check(loadedSales!.BuyerDm == -2 && loadedSales.Goods.All(g => g.Tons > 0), "Buyer DM and demand persist");
    Check(loadedSales!.IsOutdated && loadedSales.Goods.SequenceEqual(savedSales.Goods), "Sale prices reload unchanged and are marked outdated");
    Check(await marketService.GetSalesAsync(id, "1910", "Streetwise") is null && await marketService.GetSalesAsync(id, "1911", "Broker") is null, "Sales isolated by world and mode");
    var savedMarket = await marketService.RollAsync(id, "1910", "Broker", 3, "Ht");
    Check(!savedMarket.IsOutdated, "Newly rolled market is current");
    var reloadedMarket = await marketService.GetAsync(id, "1910", "Broker");
    Check(reloadedMarket!.IsOutdated, "Market loaded from database is outdated");
    Check(reloadedMarket.SupplierDm == -2 && reloadedMarket.Goods.SequenceEqual(savedMarket.Goods), "Saved market restores supplier DM and generated goods");
    Check(await marketService.GetAsync(id, "1911", "Broker") is null && await marketService.GetAsync(id, "1910", "Streetwise") is null, "Markets isolated by world and mode");
    var passengerService = new PassengerMarketService(db);
    Check(await passengerService.GetAsync(id, "1910") is null, "No passenger roll before generation");
    var passengerRoll = PassengerTraffic.Roll("A788899-C", "A", 2, 1, () => 1);
    await passengerService.SaveAsync(id, "1910", passengerRoll);
    await using (var reopened = new MtuDbContext(options))
    {
        var loadedPassengers = await new PassengerMarketService(reopened).GetAsync(id, "1910");
        Check(loadedPassengers is not null && loadedPassengers.Effect == 2 && loadedPassengers.Steward == 1
            && loadedPassengers.Lines.SequenceEqual(passengerRoll.Lines), "Passenger roll and inputs survive reopening");
        Check(await new PassengerMarketService(reopened).GetAsync(id, "1911") is null, "Passenger rolls are isolated by world");
    }
    passengerRoll = passengerRoll with { TargetDay = today };
    await passengerService.SaveAsync(id, "1910", passengerRoll);
    Check((await passengerService.GetAsync(id, "1910"))!.TargetDay == today, "Passenger target date persists");
    var selectedPassengerRoll = PassengerTraffic.Select(passengerRoll, "High", 1);
    await passengerService.SaveAsync(id, "1910", selectedPassengerRoll);
    await using (var reopened = new MtuDbContext(options))
    {
        var selection = await new PassengerMarketService(reopened).GetAsync(id, "1910");
        Check(selection!.Lines.Single(line => line.Passage == "High").Taken == 1, "Passenger selection persists");
    }
    var replacementRoll = PassengerTraffic.Roll("A788899-C", "A", -1, 3, () => 2);
    await passengerService.SaveAsync(id, "1910", replacementRoll);
    Check((await passengerService.GetAsync(id, "1910"))!.Lines.SequenceEqual(replacementRoll.Lines)
        && await db.TransportMarkets.CountAsync() == 1, "Reroll replaces the saved passenger market");
    var freightService = new FreightMarketService(db);
    Check(await freightService.GetAsync(id, "1910") is null, "Freight is separate from passenger results");
    await freightService.SaveAsync(id, "1910", freight);
    await using (var reopened = new MtuDbContext(options))
    {
        var restoredFreight = await new FreightMarketService(reopened).GetAsync(id, "1910");
        Check(restoredFreight is not null && restoredFreight.Effect == 1 && restoredFreight.Lines.SequenceEqual(freight.Lines), "Freight results persist");
        Check(await new FreightMarketService(reopened).GetAsync(id, "1911") is null, "Freight isolated by world");
    }
    var oldFreight = freight with { Lines = freight.Lines.Select(line => line.Type == "Minor"
        ? line with { LotSize = "1Dx5 (3 to 30)" } : line).ToArray() };
    await freightService.SaveAsync(id, "1910", oldFreight);
    var correctedFreight = await freightService.GetAsync(id, "1910");
    Check(correctedFreight!.Lines[1].LotSize == "1Dx5 (5 to 30)" && correctedFreight.Lines[1].Lots == oldFreight.Lines[1].Lots,
        "Legacy minor lot labels corrected without rerolling");
    loadedCargo = loadedCargo with { TargetDay = today };
    await freightService.SaveAsync(id, "1910", loadedCargo);
    Check((await freightService.GetAsync(id, "1910"))!.TargetDay == today, "Freight target date persists");
    await freightService.SaveAsync(id, "1910", loadedCargo);
    await using (var reopened = new MtuDbContext(options))
    {
        var restoredCargo = await new FreightMarketService(reopened).GetAsync(id, "1910");
        Check(restoredCargo?.CargoCapacity == 16 && restoredCargo.Lines.SequenceEqual(loadedCargo.Lines), "Cargo capacity and loaded tons persist");
    }
    await freightService.SaveAsync(id, "1910", acceptedMail);
    await using (var reopened = new MtuDbContext(options))
    {
        var savedMail = await new FreightMarketService(reopened).GetAsync(id, "1910");
        Check(savedMail?.Mail is { Rank: 2, SocDm: 1, Containers: 3, CargoTaken: 15 }, "Mail result and cargo persist");
    }
    await freightService.SaveAsync(id, "1910", sparseFreight);
    Check((await freightService.GetAsync(id, "1910"))!.Lines.SequenceEqual(sparseFreight.Lines), "Freight reroll replaces saved result");
    var oldSnapshot = reloadProvider.Catalogue;
    var mapService = reloadContainer.GetRequiredService<MapDataService>();
    Check((await mapService.GetSectorAsync(id))!.Single().Name == "Original", "Initial cached map data");
    var mapSector = (await mapService.GetOverviewAsync()).Sectors.Single();
    Check(mapSector.Borders.Single().Color == "lightblue" && mapSector.Routes.Single().EndOffsetX == 1, "Map overview exposes imported border and route geometry");
    var notifications = 0;
    reloadProvider.Reloaded += () => notifications++;
    mock.Version = 2;
    await worker.RunAsync(progress);
    db.ChangeTracker.Clear();
    Check((await db.Sectors.SingleAsync()).Id == id && (await db.Systems.SingleAsync()).Name == "Updated", "Existing sector fully refreshed with stable sector ID");
    Check((await db.Borders.SingleAsync()).Color == "red" && (await db.Routes.SingleAsync()).Color == "orange", "Explicit colours override stylesheet");
    Check((await marketService.GetAsync(id, "1910", "Broker"))!.Goods.SequenceEqual(savedMarket.Goods), "Market survives sector system-row replacement");
    Check((await passengerService.GetAsync(id, "1910"))!.Lines.SequenceEqual(replacementRoll.Lines), "Passenger results survive system refresh");
    await reloadProvider.ReloadAsync();
    Check(notifications == 1 && !ReferenceEquals(oldSnapshot, reloadProvider.Catalogue), "Reload publishes one new catalogue");
    Check(oldSnapshot.Systems.Single().MainWorld.Name == "Original", "Readers retain an intact old snapshot");
    Check((await mapService.GetSectorAsync(id))!.Single().Name == "Updated", "Existing map service rebuilds after reload");
    var refreshedSector = (await mapService.GetOverviewAsync()).Sectors.Single();
    Check(refreshedSector.Worlds.Single().Name == "Updated" && refreshedSector.Borders.Single().Color == "red" && refreshedSector.Routes.Single().Color == "orange", "Overview rebuilds world and geometry after reload");
    Check((await reloadContainer.GetRequiredService<ISearchService>().SearchAsync("Updated")).Single().SystemName == "Updated", "Search sees reloaded system");
    Check((await reloadContainer.GetRequiredService<WorldSelectionService>().FindAsync(-4,-1,19,10))!.Name == "Updated", "Selection sees reloaded system");
    mock.FailMetadata = true;
    await worker.RunAsync(progress);
    db.ChangeTracker.Clear();
    Check((await db.Systems.SingleAsync()).Name == "Updated" && reports.Last().Failed == 1, "Failed metadata fetch retains previous sector");
    var validSnapshot = reloadProvider.Catalogue;
    await importConnection.CloseAsync();
    try { await reloadProvider.ReloadAsync(); throw new Exception("Reload unexpectedly succeeded without database"); }
    catch (Microsoft.Data.Sqlite.SqliteException) { }
    Check(ReferenceEquals(validSnapshot, reloadProvider.Catalogue) && notifications == 1, "Failed reload preserves the published snapshot and sends no notification");
    Check(MetadataStyles.Color("/* note */ border.Foo, border.Bar { color: blue; } border.Bar { color: red }", "border", "Bar") == "red", "Stylesheet grouped selectors and order");
}
Console.WriteLine("Metadata migration, full refresh, styles, cross-sector routes and failed-fetch preservation passed.");

sealed class InlineProgress<T>(Action<T> action) : IProgress<T> { public void Report(T value) => action(value); }
sealed class RefreshClient(SectorSummary sector) : ITravellerMapClient
{
    public int Version = 1; public bool FailMetadata;
    public Task<IReadOnlyList<SectorSummary>> GetSectorsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<SectorSummary>>([sector]);
    public Task<SectorData> GetSectorDataAsync(string name, CancellationToken ct = default) => Task.FromResult(new SectorData {
        Systems = [new StarSystem { Hex = "1910", Name = Version == 1 ? "Original" : "Updated" }] });
    public Task<SectorMetadata> GetMetadataAsync(string name, string milieu, CancellationToken ct = default)
    {
        if (FailMetadata) throw new HttpRequestException("test failure");
        return Task.FromResult(new SectorMetadata {
            Stylesheet = "border.SwCf { color: lightblue; } route.SwCf { color: #006600; }",
            Subsectors = [new MetadataSubsector { Index = "C", Name = "Regina" }],
            Allegiances = [new MetadataAllegiance { Code = "SwCf", Name = "Sword Worlds", ShortName = "Sword Worlds" }],
            Borders = [new MetadataBorder { Path = "0000 0100 0000", Allegiance = "SwCf", Color = Version == 2 ? "red" : null }],
            Routes = [new MetadataRoute { Start = "3209", End = "0208", EndOffsetX = 1, Allegiance = "SwCf", Color = Version == 2 ? "orange" : null }] });
    }
}

sealed class CatalogueCommandCounter : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
{
    public int Reads;
    public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
        Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<System.Data.Common.DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Reads);
        return ValueTask.FromResult(result);
    }
}

