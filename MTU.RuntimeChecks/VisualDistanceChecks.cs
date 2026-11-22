using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class VisualDistanceChecks
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

        Check(
            LineOfSight.SightMetres(24, 0) == 24
                && LineOfSight.SightMetres(0, 12) == 12
                && LineOfSight.SightMetres(9, 30) == 30
                && LineOfSight.SightMetres(30, 9) == 30
                && LineOfSight.SightMetres(0, 0) == 0,
            "The higher of the two distances is used, and 0 for both clears nothing"
        );

        using (var connection = new SqliteConnection("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddDbContext<MtuDbContext>(o => o.UseSqlite(connection));
            services.AddScoped<SceneService>();
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
            await db.Database.MigrateAsync();
            db.Campaigns.AddRange(
                new CampaignEntity { Id = 1, Name = "Home" },
                new CampaignEntity { Id = 2, Name = "Away" }
            );
            await db.SaveChangesAsync();
            var scenes = scope.ServiceProvider.GetRequiredService<SceneService>();
            var scene = await scenes.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Sight",
                    GridSize = 100,
                },
                SceneImageChecks.Png(250, 180)
            );

            // The scene's Ambient Visual Distance.
            Check(
                await scenes.AmbientVisualDistanceAsync(1, scene.Id) == 0,
                "A new scene has no Ambient Visual Distance"
            );
            await scenes.SetAmbientVisualDistanceAsync(1, scene.Id, 24);
            Check(
                await new SceneService(
                    provider.GetRequiredService<IServiceScopeFactory>()
                ).AmbientVisualDistanceAsync(1, scene.Id) == 24,
                "The Ambient Visual Distance is saved with the scene"
            );
            await Reject(
                () => scenes.SetAmbientVisualDistanceAsync(1, scene.Id, -1),
                "A negative Ambient Visual Distance is rejected"
            );
            await Reject(
                () =>
                    scenes.SetAmbientVisualDistanceAsync(
                        1,
                        scene.Id,
                        LineOfSight.MaxVisualDistance + 1
                    ),
                "An Ambient Visual Distance over the limit is rejected"
            );
            await Reject(
                () => scenes.SetAmbientVisualDistanceAsync(2, scene.Id, 6),
                "Setting the Ambient Visual Distance respects campaigns"
            );
            await Reject(
                async () =>
                {
                    await scenes.AmbientVisualDistanceAsync(2, scene.Id);
                },
                "Reading the Ambient Visual Distance respects campaigns"
            );
            Check(
                await scenes.AmbientVisualDistanceAsync(1, scene.Id) == 24,
                "Rejected changes leave the Ambient Visual Distance alone"
            );
            await scenes.SaveGridAsync(1, scene.Id, "#ffffff", 1, 20, 50);
            await scenes.SaveAsync(
                new SceneInput
                {
                    Id = scene.Id,
                    CampaignId = 1,
                    Name = "Sight",
                    GridSize = 50,
                },
                SceneImageChecks.Png(300, 200)
            );
            Check(
                await scenes.AmbientVisualDistanceAsync(1, scene.Id) == 24,
                "A new grid size or background keeps the Ambient Visual Distance"
            );
            await scenes.SetAmbientVisualDistanceAsync(1, scene.Id, LineOfSight.MaxVisualDistance);
            await scenes.SetAmbientVisualDistanceAsync(1, scene.Id, 0);
            Check(
                await scenes.AmbientVisualDistanceAsync(1, scene.Id) == 0,
                "The Ambient Visual Distance can be set back to 0"
            );

            // A Traveller's own Visual Distance.
            var characters = new CharacterService(db);
            var id = await characters.SaveAsync(
                new Character(0, 1, "Player", "Scout", "", "777777", 0)
            );
            async Task<Character> Scout() =>
                (await new CharacterService(db).ListAsync(1)).Single(c => c.Id == id);
            Check((await Scout()).VisualDistance == 0, "A new Traveller has no Visual Distance");
            await characters.SaveAsync(await Scout() with { VisualDistance = 12 });
            Check((await Scout()).VisualDistance == 12, "A Traveller's Visual Distance is saved");
            await Reject(
                async () => await characters.SaveAsync(await Scout() with { VisualDistance = -1 }),
                "A negative Visual Distance is rejected"
            );
            await Reject(
                async () =>
                    await characters.SaveAsync(
                        await Scout() with
                        {
                            VisualDistance = LineOfSight.MaxVisualDistance + 1,
                        }
                    ),
                "A Visual Distance over the limit is rejected"
            );
            await characters.SaveAsync(await Scout() with { Speed = 9 });
            Check(
                (await Scout()) is { VisualDistance: 12, Speed: 9 },
                "Other edits keep the Visual Distance"
            );
            await scenes.MoveTravellersHereAsync(1, scene.Id);
            Check(
                (await scenes.CombatantsAsync(1, scene.Id)).Single(c => c.CharacterId == id)
                    is { VisualDistance: 12, IsNpc: false },
                "The Battlemap's combatant carries the Traveller's Visual Distance"
            );
        }

        // Migration: both columns are added at 0 to existing rows and removed again on rollback.
        using (var connection = new SqliteConnection("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddDbContext<MtuDbContext>(o => o.UseSqlite(connection));
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
            var migrator = db.GetService<IMigrator>();
            const string previous = "20261122020000_ScenePolygons";
            Task<int> Count(string sql) => db.Database.SqlQueryRaw<int>(sql).SingleAsync();
            Task<int> Columns() =>
                Count(
                    "SELECT (SELECT COUNT(*) FROM pragma_table_info('Scenes') WHERE name = 'AmbientVisualDistance') + (SELECT COUNT(*) FROM pragma_table_info('Characters') WHERE name = 'VisualDistance') AS Value"
                );
            await migrator.MigrateAsync(previous);
            Check(await Columns() == 0, "The distances are not stored before the migration");
            // Insert with SQL: both tables are still at the older schema.
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Campaigns (Id, Name, Day, Year, IsActive) VALUES (1, 'Legacy', 1, 1105, 1);
                INSERT INTO Characters (Id, CampaignId, Name, PlayerName, Race, Upp, NaturalDr, Inactive, IsNpc, IsAnimal, InjuryTolerance, Speed, Description)
                    VALUES (1, 1, 'Old hand', 'Player', '', '777777', 0, 0, 0, 0, 0, 9, '');
                INSERT INTO Scenes (Id, CampaignId, Name, Image, ContentType, GridSize, GridColour, GridOpacity, GridlineWidth, FogEnabled, Revision)
                    VALUES (1, 1, 'Old scene', x'00', 'image/png', 100, '#000000', 50, 2, 1, 3);
                """
            );
            var scenes = new SceneService(provider.GetRequiredService<IServiceScopeFactory>());
            for (var pass = 0; pass < 2; pass++)
            {
                await migrator.MigrateAsync();
                Check(await Columns() == 2, "The migration adds both distance columns");
                Check(
                    await scenes.AmbientVisualDistanceAsync(1, 1) == 0
                        && (await new CharacterService(db).ListAsync(1)).Single()
                            is { Name: "Old hand", Speed: 9, VisualDistance: 0 },
                    "Existing scenes and characters start at 0 and keep their data"
                );
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE Scenes SET AmbientVisualDistance = 24; UPDATE Characters SET VisualDistance = 12;"
                );
                Check(
                    await scenes.AmbientVisualDistanceAsync(1, 1) == 24
                        && (await new CharacterService(db).ListAsync(1)).Single().VisualDistance
                            == 12,
                    "Stored distances are read back"
                );
                await migrator.MigrateAsync(previous);
                Check(
                    await Columns() == 0
                        && await Count(
                            "SELECT COUNT(*) AS Value FROM Scenes WHERE Name = 'Old scene' AND Revision = 3"
                        ) == 1
                        && await Count(
                            "SELECT COUNT(*) AS Value FROM Characters WHERE Name = 'Old hand' AND Speed = 9"
                        ) == 1,
                    "Rolling back removes both columns and keeps the scene and the character"
                );
            }
        }
        Console.WriteLine(
            "Visual distance migration and rollback, ambient and character distances, limits, campaign isolation, scene edits and the combatant value passed."
        );
    }
}
