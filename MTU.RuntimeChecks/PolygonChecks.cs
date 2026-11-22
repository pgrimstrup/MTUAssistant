using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class PolygonChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        static ScenePolygon Polygon(params (int X, int Y)[] points) =>
            new([.. points.Select(p => new PolygonPoint(p.X, p.Y))]);
        static bool Same(IReadOnlyList<ScenePolygon> actual, params ScenePolygon[] expected) =>
            actual.Count == expected.Length
            && actual
                .Zip(expected)
                .All(pair =>
                    pair.First.Points.SequenceEqual(pair.Second.Points)
                    && pair.First.Active == pair.Second.Active
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
            // 250 x 180 pixels at 100 per square: half squares run 0-5 across and 0-3 down.
            var scene = await scenes.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Polygons",
                    GridSize = 100,
                },
                SceneImageChecks.Png(250, 180)
            );
            Check(
                (await scenes.LoadPolygonsAsync(1, scene.Id)).Count == 0,
                "New scenes have no polygons"
            );
            var triangle = Polygon((0, 0), (4, 0), (2, 2));
            var centres = Polygon((1, 1), (3, 1), (5, 3), (1, 3));
            Task Save(params ScenePolygon[] polygons) =>
                scenes.SavePolygonsAsync(1, scene.Id, scene.Revision, 100, polygons);
            await Save(triangle, centres);
            var restored = await new SceneService(
                provider.GetRequiredService<IServiceScopeFactory>()
            ).LoadPolygonsAsync(1, scene.Id);
            Check(
                Same(restored, triangle, centres),
                "Polygons persist in order, on corners, centres and the image edge"
            );
            Check(restored.All(p => p.Active), "New polygons are active");
            var open = triangle with { Active = false };
            await Save(open, centres);
            Check(
                Same(await scenes.LoadPolygonsAsync(1, scene.Id), open, centres),
                "An inactive polygon stays inactive when saved"
            );
            await Save(triangle, centres);
            async Task Reject(Func<Task> operation, string message)
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
            await Reject(
                () => scenes.SavePolygonsAsync(2, scene.Id, scene.Revision, 100, []),
                "Polygon saves respect campaigns"
            );
            await Reject(
                async () =>
                {
                    await scenes.LoadPolygonsAsync(2, scene.Id);
                },
                "Polygon reads respect campaigns"
            );
            await Reject(() => Save(Polygon((0, 0), (2, 0))), "Two points are not a polygon");
            await Reject(
                () => Save(Polygon((0, 0), (1, 0), (2, 2))),
                "Points between a corner and a centre are rejected"
            );
            await Reject(
                () => Save(Polygon((0, 0), (6, 0), (2, 2))),
                "Points right of the image are rejected"
            );
            await Reject(
                () => Save(Polygon((0, 0), (4, 0), (2, 4))),
                "Points below the image are rejected"
            );
            await Reject(
                () => Save(Polygon((0, 0), (4, 0), (-2, 2))),
                "Negative points are rejected"
            );
            await Reject(
                () => Save(Polygon((0, 0), (4, 0), (4, 0), (2, 2))),
                "Repeated points are rejected"
            );
            await Reject(
                () => Save(Polygon((0, 0), (4, 0), (2, 2), (0, 0))),
                "A polygon does not repeat its first point to close"
            );
            await Reject(
                () =>
                    Save(
                        Polygon([
                            .. Enumerable
                                .Range(0, SceneService.MaxPolygonPoints + 1)
                                .Select(i => (i % 2 * 2, i % 4 / 2 * 2)),
                        ])
                    ),
                "Polygons with too many points are rejected"
            );
            await Reject(
                () => Save([.. Enumerable.Repeat(triangle, SceneService.MaxPolygons + 1)]),
                "Too many polygons are rejected"
            );
            await Reject(
                () => Save(new ScenePolygon(null!)),
                "Polygons without points are rejected"
            );
            await Reject(
                () => scenes.SavePolygonsAsync(1, scene.Id, scene.Revision + 1, 100, [triangle]),
                "Stale image rejected"
            );
            await Reject(
                () => scenes.SavePolygonsAsync(1, scene.Id, scene.Revision, 50, [triangle]),
                "Stale grid rejected"
            );
            Check(
                Same(await scenes.LoadPolygonsAsync(1, scene.Id), triangle, centres),
                "Invalid saves preserve polygons"
            );
            await Save();
            Check(
                (await scenes.LoadPolygonsAsync(1, scene.Id)).Count == 0
                    && await db
                        .Scenes.AsNoTracking()
                        .Where(s => s.Id == scene.Id)
                        .Select(s => s.PolygonState)
                        .SingleAsync()
                        is null,
                "Deleting the last polygon clears the stored state"
            );
            await Save(triangle);
            await scenes.SaveFogAsync(1, scene.Id, scene.Revision, 100, [new(0, 0)]);
            Check(
                Same(await scenes.LoadPolygonsAsync(1, scene.Id), triangle)
                    && (await scenes.LoadFogAsync(1, scene.Id)).Count == 1,
                "Fog and polygons are saved independently"
            );
            await scenes.SaveGridAsync(1, scene.Id, "#ffffff", 1, 20, 100);
            Check(
                Same(await scenes.LoadPolygonsAsync(1, scene.Id), triangle),
                "Grid styling preserves polygons"
            );
            await scenes.SaveGridAsync(1, scene.Id, "#ffffff", 1, 20, 50);
            Check(
                (await scenes.LoadPolygonsAsync(1, scene.Id)).Count == 0,
                "Grid size changes reset polygons"
            );
            // At 50 per square the same image is 10 half squares across and 7 down.
            var wide = Polygon((0, 0), (10, 0), (9, 7), (1, 7));
            await scenes.SavePolygonsAsync(1, scene.Id, scene.Revision, 50, [wide]);
            var renamed = await scenes.SaveAsync(
                new SceneInput
                {
                    Id = scene.Id,
                    CampaignId = 1,
                    Name = "Renamed",
                    GridSize = 50,
                }
            );
            Check(
                Same(await scenes.LoadPolygonsAsync(1, scene.Id), wide),
                "Scene edits that keep the image and grid size keep polygons"
            );
            await Reject(
                () => scenes.SavePolygonsAsync(1, scene.Id, scene.Revision, 50, [wide]),
                "Saves from before a scene edit are rejected"
            );
            await scenes.SavePolygonsAsync(1, scene.Id, renamed.Revision, 50, [wide, wide]);
            await scenes.SaveAsync(
                new SceneInput
                {
                    Id = scene.Id,
                    CampaignId = 1,
                    Name = "Renamed",
                    GridSize = 50,
                },
                SceneImageChecks.Png(300, 200)
            );
            Check(
                (await scenes.LoadPolygonsAsync(1, scene.Id)).Count == 0,
                "Replacement backgrounds reset polygons"
            );
        }

        // Migration: the column is added to existing scenes and removed again on rollback.
        using (var connection = new SqliteConnection("Data Source=:memory:"))
        {
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddDbContext<MtuDbContext>(o => o.UseSqlite(connection));
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
            var migrator = db.GetService<IMigrator>();
            const string previous = "20261122010000_CareerTerms";
            Task<int> ColumnCount() =>
                db
                    .Database.SqlQueryRaw<int>(
                        "SELECT COUNT(*) AS Value FROM pragma_table_info('Scenes') WHERE name = 'PolygonState'"
                    )
                    .SingleAsync();
            await migrator.MigrateAsync(previous);
            Check(await ColumnCount() == 0, "Polygons are not stored before the migration");
            // Insert with SQL: the Scenes table is still at the older schema. The JSON is passed as a
            // parameter because raw SQL treats braces as placeholders.
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Campaigns (Id, Name, Day, Year, IsActive) VALUES (1, 'Legacy', 1, 1105, 1);
                INSERT INTO Scenes (Id, CampaignId, Name, Image, ContentType, GridSize, GridColour, GridOpacity, GridlineWidth, FogState, FogEnabled, Revision)
                    VALUES (1, 1, 'Old scene', x'00', 'image/png', 100, '#000000', 50, 2, {0}, 1, 3);
                """,
                """[{"Column":0,"Row":0}]"""
            );
            var scenes = new SceneService(provider.GetRequiredService<IServiceScopeFactory>());
            for (var pass = 0; pass < 2; pass++)
            {
                await migrator.MigrateAsync();
                Check(await ColumnCount() == 1, "The migration adds the polygon column");
                Check(
                    (await scenes.LoadPolygonsAsync(1, 1)).Count == 0
                        && (await scenes.LoadFogAsync(1, 1)).Count == 1
                        && await scenes.FogEnabledAsync(1, 1),
                    "Existing scenes keep their fog and start with no polygons"
                );
                await db.Database.ExecuteSqlRawAsync(
                    "UPDATE Scenes SET PolygonState = {0} WHERE Id = 1;",
                    """[{"Points":[{"X":0,"Y":0},{"X":2,"Y":0},{"X":1,"Y":1}]}]"""
                );
                Check(
                    Same(await scenes.LoadPolygonsAsync(1, 1), Polygon((0, 0), (2, 0), (1, 1))),
                    "Stored polygons are read back, and are active when no flag was stored"
                );
                await migrator.MigrateAsync(previous);
                Check(
                    await ColumnCount() == 0
                        && await db
                            .Database.SqlQueryRaw<int>(
                                "SELECT COUNT(*) AS Value FROM Scenes WHERE Name = 'Old scene' AND Revision = 3"
                            )
                            .SingleAsync() == 1,
                    "Rolling back removes the polygon column and keeps the scene"
                );
            }
        }
        Console.WriteLine(
            "Polygon migration and rollback, persistence, the active flag, snap-point and bounds validation, limits, campaign isolation, stale saves and grid/image resets passed."
        );
    }
}
