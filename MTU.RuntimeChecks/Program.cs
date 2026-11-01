using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

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
Check(commands.Reads == 4, "Concurrent initialization reads each catalogue table exactly once");
var snapshot = catalogueProvider.Catalogue;

await catalogueProvider.InitializeAsync();
Check(ReferenceEquals(snapshot, catalogueProvider.Catalogue) && commands.Reads == 4, "Initialization reuses the published snapshot");
Check(snapshot.Systems.Length == 4 && snapshot.Systems.All(s => s.Bodies.IsEmpty), "All milieus loaded with no invented system bodies");
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
