using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class StarshipChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        static async Task Reject(Func<Task> operation, string message)
        {
            var rejected = false;
            try
            {
                await operation();
            }
            catch (ArgumentException)
            {
                rejected = true;
            }
            Check(rejected, message);
        }
        static StarshipSystem Part(
            StarshipSystemType type,
            int rating = 0,
            decimal tons = 0,
            decimal power = 0,
            decimal cost = 0,
            string description = ""
        ) => new(0, type, description, tons, cost, power, rating);

        // Performance from the systems.
        StarshipSystem[] trader =
        [
            Part(StarshipSystemType.Cargo, tons: 40.5m, description: "Main hold"),
            Part(StarshipSystemType.Hull, rating: 80, cost: 10, description: "Standard hull"),
            Part(StarshipSystemType.Hull, rating: 8, description: "Reinforced"),
            Part(StarshipSystemType.MDrive, rating: 1, tons: 2, power: 20),
            Part(StarshipSystemType.MDrive, rating: 2, tons: 4, power: 40),
            Part(StarshipSystemType.JDrive, rating: 1, tons: 10, power: 20, cost: 15.2345m),
            Part(StarshipSystemType.PowerPlant, rating: 60, tons: 4),
            Part(StarshipSystemType.PowerPlant, rating: 30, tons: 2),
            Part(StarshipSystemType.Sensors, tons: 1, power: 0.5m),
            Part(StarshipSystemType.Cargo, tons: 12, description: "Forward hold"),
        ];
        var performance = StarshipRules.Performance(trader);
        Check(performance.HullPoints == 88, "Hull Points add up the Hull systems' ratings");
        Check(
            StarshipRules.PurchaseCost(trader, prototype: false) == 22.71105m
                && StarshipRules.PurchaseCost(trader, prototype: true) == 25.2345m,
            "Purchase Cost is 90% of the systems' cost, or all of it for a prototype"
        );
        Check(
            StarshipRules.MaintenanceCost(22.71105m) == 1892
                && StarshipRules.MaintenanceCost(25.2345m) == 2102
                && StarshipRules.MaintenanceCost(0.0119m) == 0
                && StarshipRules.MaintenanceCost(0.012m) == 1,
            "Maintenance is the Purchase Cost in credits ÷ 12,000, rounded down"
        );
        Check(
            performance.Thrust == 2 && performance.Jump == 1,
            "The M-Drive and J-Drive ratings are the best drive of each kind"
        );
        Check(performance.PowerOutput == 90, "Power plants add their output together");
        Check(performance.PowerRequired == 80.5m, "Power needed adds every system's requirement");
        Check(performance.CargoTons == 52.5m, "Cargo space adds up the Cargo systems' tons");
        Check(performance.TonsAllocated == 75.5m, "Tons used adds up every system");
        var empty = StarshipRules.Performance([]);
        Check(
            empty.HullPoints == 0
                && empty.Thrust == 0
                && empty.Jump == 0
                && empty.PowerOutput == 0
                && empty.CargoTons == 0,
            "A ship without systems has no hull, drives, power or cargo"
        );
        Check(
            StarshipRules.StandardHullPoints(200) == 80
                && StarshipRules.StandardHullPoints(100) == 40
                && StarshipRules.StandardHullPoints(101) == 40,
            "A standard hull has one Hull Point per 2.5 tons, rounded down"
        );
        Check(
            StarshipRules.Label(StarshipSystemType.MDrive) == "M-Drive"
                && StarshipRules.Label(StarshipSystemType.CommonAreas) == "Common Areas"
                && StarshipRules.SystemTypes[0] == StarshipSystemType.Hull
                && StarshipRules.SystemTypes[^1] == StarshipSystemType.Cargo
                && StarshipRules.SystemTypes.Count == 14,
            "System types are labelled and listed from Hull to Cargo"
        );

        // Crew text.
        var crew = StarshipRules.ParseCrew("Pilot, Engineer x3, Gunner ×2 ,, Steward X1, Medic");
        Check(
            crew.Select(p => $"{p.Name}:{p.Count}")
                .SequenceEqual(["Pilot:1", "Engineer:3", "Gunner:2", "Steward:1", "Medic:1"]),
            "Crew positions read optional x, X and × multipliers and skip blanks"
        );
        Check(
            StarshipRules.CrewCount("Pilot, Engineer x3") == 4
                && StarshipRules.CrewCount("") == 0
                && StarshipRules.CrewCount(null) == 0,
            "The crew count adds the multipliers"
        );
        Check(
            StarshipRules.ParseCrew("x3").Single() == new StarshipCrewPosition("x3", 1),
            "A multiplier without a position name is kept as the name"
        );

        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<MtuDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<StarshipService>();
        using var provider = services.BuildServiceProvider();
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
            await db.Database.MigrateAsync();
            db.Campaigns.AddRange(
                new CampaignEntity { Id = 1, Name = "Home" },
                new CampaignEntity { Id = 2, Name = "Away" }
            );
            await db.SaveChangesAsync();
            var ships = scope.ServiceProvider.GetRequiredService<StarshipService>();
            Starship New(
                int campaignId,
                string name,
                IReadOnlyList<StarshipSystem>? systems = null
            ) =>
                new(
                    0,
                    campaignId,
                    name,
                    12,
                    200,
                    0,
                    false,
                    "Pilot, Engineer",
                    false,
                    systems ?? []
                );

            var beowulf = await ships.SaveAsync(
                New(1, "  Beowulf  ", trader) with
                {
                    Equipped = true,
                    Classification = " Free Trader ",
                }
            );
            Check(
                beowulf.Id > 0 && beowulf.Name == "Beowulf",
                "A new ship is saved with its name trimmed"
            );
            Check(
                beowulf.Classification == "Free Trader"
                    && (await ships.ListAsync(1)).Single().Classification == "Free Trader",
                "A ship's Type is saved trimmed and listed"
            );
            Check(!beowulf.Equipped, "Saving a ship never equips it");
            Check(
                !beowulf.Prototype
                    && beowulf.PurchaseCost == 22.71105m
                    && beowulf.MaintenanceCost == 1892
                    && await db.Starships.AsNoTracking().SingleAsync(s => s.Id == beowulf.Id)
                        is { PurchaseCost: 22.71105m, MaintenanceCost: 1892m },
                "A saved ship's costs are worked out from its systems and stored"
            );
            var prototype = await ships.SaveAsync(beowulf with { Prototype = true });
            Check(
                prototype.Prototype
                    && prototype.PurchaseCost == 25.2345m
                    && prototype.MaintenanceCost == 2102,
                "A prototype pays full price"
            );
            beowulf = await ships.SaveAsync(prototype with { Prototype = false });
            Check(
                beowulf
                    .Systems.Select(s => s.Type)
                    .SequenceEqual(trader.Select(s => s.Type).Order())
                    && beowulf
                        .Systems.Where(s => s.Type == StarshipSystemType.Cargo)
                        .Select(s => s.Description)
                        .SequenceEqual(["Main hold", "Forward hold"])
                    && beowulf
                        .Systems.Where(s => s.Type == StarshipSystemType.Hull)
                        .Select(s => s.Description)
                        .SequenceEqual(["Standard hull", "Reinforced"]),
                "Systems are listed by type and keep their order within a type"
            );
            Check(
                beowulf.Performance == performance,
                "A saved ship's performance matches its systems"
            );

            // Editing replaces the systems.
            var edited = await ships.SaveAsync(
                beowulf with
                {
                    DamageTaken = 100,
                    Systems =
                    [
                        Part(StarshipSystemType.Hull, rating: 80),
                        Part(StarshipSystemType.JDrive, rating: 2),
                    ],
                }
            );
            Check(
                edited.Id == beowulf.Id
                    && edited.Systems.Count == 2
                    && edited.Performance.Jump == 2
                    && edited.Performance.CargoTons == 0
                    && await db.StarshipSystems.CountAsync() == 2,
                "Saving a ship replaces all its systems"
            );
            Check(edited.HullRemaining == 0, "Hull remaining never drops below 0");

            // Validation.
            await Reject(() => ships.SaveAsync(New(1, " ")), "A ship needs a name");
            await Reject(
                () =>
                    ships.SaveAsync(New(1, "Typed") with { Classification = new string('T', 121) }),
                "A Type over 120 characters is rejected"
            );
            await Reject(
                () => ships.SaveAsync(New(1, "Low") with { TechLevel = 8 }),
                "Tech Level 8 is rejected"
            );
            await Reject(
                () => ships.SaveAsync(New(1, "High") with { TechLevel = 26 }),
                "Tech Level 26 is rejected"
            );
            await Reject(
                () => ships.SaveAsync(New(1, "Small") with { Displacement = 99 }),
                "Displacement under 100 tons is rejected"
            );
            await Reject(
                () => ships.SaveAsync(New(1, "Huge") with { Displacement = 500_001 }),
                "Displacement over 500,000 tons is rejected"
            );
            await Reject(
                () => ships.SaveAsync(New(1, "Hurt") with { DamageTaken = -1 }),
                "Negative damage is rejected"
            );
            await Reject(
                () =>
                    ships.SaveAsync(
                        New(1, "Cheap", [Part(StarshipSystemType.Bridge, cost: -0.001m)])
                    ),
                "A negative system cost is rejected"
            );
            await Reject(
                () => ships.SaveAsync(New(1, "Odd", [Part((StarshipSystemType)99)])),
                "An unknown system type is rejected"
            );
            await Reject(
                () => ships.SaveAsync(New(1, "Odd", [Part(StarshipSystemType.Cargo, tons: -1)])),
                "Negative system tons are rejected"
            );
            await Reject(
                () => ships.SaveAsync(New(1, "Odd", [Part(StarshipSystemType.MDrive, rating: -1)])),
                "A negative rating is rejected"
            );
            await Reject(
                () =>
                    ships.SaveAsync(
                        New(
                            1,
                            "Busy",
                            Enumerable
                                .Repeat(
                                    Part(StarshipSystemType.Software),
                                    StarshipRules.MaxSystems + 1
                                )
                                .ToArray()
                        )
                    ),
                "Too many systems are rejected"
            );
            Check(
                await ships.SaveAsync(
                    New(1, "Bounds") with
                    {
                        TechLevel = 25,
                        Displacement = 500_000,
                    }
                )
                    is { TechLevel: 25 },
                "The highest Tech Level and displacement are allowed"
            );
            await Reject(
                () => ships.SaveAsync(New(3, "Nowhere")),
                "A ship needs an existing campaign"
            );

            // Equipped: one ship per campaign.
            var scout = await ships.SaveAsync(New(1, "Scout"));
            var away = await ships.SaveAsync(New(2, "Away ship"));
            await ships.SetEquippedAsync(1, beowulf.Id, true);
            await ships.SetEquippedAsync(2, away.Id, true);
            Check(
                (await ships.EquippedAsync(1))?.Id == beowulf.Id,
                "Equipping a ship makes it the party's ship"
            );
            await ships.SetEquippedAsync(1, scout.Id, true);
            Check(
                (await ships.EquippedAsync(1))?.Id == scout.Id
                    && (await ships.ListAsync(1)).Count(s => s.Equipped) == 1,
                "Equipping another ship unequips the first"
            );
            Check(
                (await ships.EquippedAsync(2))?.Id == away.Id,
                "Each campaign has its own equipped ship"
            );
            await ships.SetEquippedAsync(1, beowulf.Id, false);
            Check(
                (await ships.EquippedAsync(1))?.Id == scout.Id,
                "Unequipping a ship that isn't equipped changes nothing"
            );
            await ships.SaveAsync(
                (await ships.GetAsync(1, scout.Id))! with
                {
                    Name = "Scout II",
                    Equipped = false,
                }
            );
            Check(
                (await ships.EquippedAsync(1))?.Name == "Scout II",
                "Editing the equipped ship keeps it equipped"
            );
            await ships.SetEquippedAsync(1, scout.Id, false);
            Check(
                await ships.EquippedAsync(1) is null,
                "Unequipping the ship leaves none equipped"
            );
            var duplicate = false;
            try
            {
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE Starships SET Equipped = 1 WHERE CampaignId = 1"
                );
            }
            catch (SqliteException)
            {
                duplicate = true;
            }
            Check(duplicate, "The database allows only one equipped ship per campaign");

            // Campaign isolation.
            await Reject(
                () => ships.SaveAsync(beowulf with { CampaignId = 2 }),
                "A ship can't be saved into another campaign"
            );
            await Reject(
                () => ships.SetEquippedAsync(2, beowulf.Id, true),
                "A ship can't be equipped from another campaign"
            );
            await Reject(
                () => ships.DeleteAsync(2, beowulf.Id),
                "A ship can't be deleted from another campaign"
            );
            Check(
                await ships.GetAsync(2, beowulf.Id) is null,
                "A ship can't be read from another campaign"
            );
            Check(
                (await ships.ListAsync(2)).Select(s => s.Name).SequenceEqual(["Away ship"]),
                "The list shows only the campaign's ships"
            );
            Check(
                (await ships.ListAsync(1))
                    .Select(s => s.Name)
                    .SequenceEqual(["Beowulf", "Bounds", "Scout II"]),
                "Ships are listed by name"
            );

            // Delete.
            await ships.DeleteAsync(1, beowulf.Id);
            Check(
                await ships.GetAsync(1, beowulf.Id) is null
                    && !await db.StarshipSystems.AnyAsync(s => s.StarshipId == beowulf.Id),
                "Deleting a ship deletes its systems"
            );
            await Reject(
                () => ships.DeleteAsync(1, beowulf.Id),
                "A deleted ship can't be deleted again"
            );
            db.ChangeTracker.Clear();
            db.Campaigns.Remove(await db.Campaigns.SingleAsync(c => c.Id == 2));
            await db.SaveChangesAsync();
            Check(
                !await db.Starships.AnyAsync(s => s.CampaignId == 2),
                "Deleting a campaign deletes its ships"
            );

            // The migration rolls back and forward again.
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261122040000_InjuryToleranceRecords");
            var tables = await db
                .Database.SqlQueryRaw<string>(
                    "SELECT name AS Value FROM sqlite_master WHERE type = 'table' AND name LIKE 'Starship%'"
                )
                .ToListAsync();
            Check(tables.Count == 0, "Rolling back the Starships migration drops its tables");
            await migrator.MigrateAsync();
            Check(
                !await db.Starships.AnyAsync(),
                "The Starships migration applies again after a rollback"
            );
        }
        Console.WriteLine(
            "Starship performance, crew, saving, validation, equipping, campaign isolation, deletion and migration checks passed."
        );
    }
}
