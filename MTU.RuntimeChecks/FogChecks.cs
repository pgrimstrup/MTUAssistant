using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class FogChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        using var connection = new SqliteConnection("Data Source=:memory:");
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
                Name = "Fog",
                GridSize = 100,
            },
            SceneImageChecks.Png(250, 180)
        );
        Check((await scenes.LoadFogAsync(1, scene.Id)).Count == 0, "New scenes start covered");
        Check(!await scenes.FogEnabledAsync(1, scene.Id), "Fog is off by default");
        await scenes.SetFogEnabledAsync(1, scene.Id, true);
        Check(await scenes.FogEnabledAsync(1, scene.Id), "Fog setting persists");
        await scenes.SaveFogAsync(
            1,
            scene.Id,
            scene.Revision,
            100,
            [new(0, 0), new(2, 1), new(0, 0)]
        );
        var restored = await new SceneService(
            provider.GetRequiredService<IServiceScopeFactory>()
        ).LoadFogAsync(1, scene.Id);
        Check(
            restored.SequenceEqual(new FogCell[] { new(0, 0), new(2, 1) }),
            "Fog persists, deduplicates and includes partial edge cells"
        );
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
            () => scenes.SaveFogAsync(2, scene.Id, scene.Revision, 100, []),
            "Campaign isolation"
        );
        await Reject(
            async () =>
            {
                await scenes.LoadFogAsync(2, scene.Id);
            },
            "Fog reads respect campaigns"
        );
        await Reject(
            () => scenes.SaveFogAsync(1, scene.Id, scene.Revision, 100, [new(3, 0)]),
            "Out of map cells rejected"
        );
        await Reject(
            () => scenes.SaveFogAsync(1, scene.Id, scene.Revision, 100, [new(-1, 0)]),
            "Negative cells rejected"
        );
        await Reject(
            () => scenes.SaveFogAsync(1, scene.Id, scene.Revision + 1, 100, []),
            "Stale image rejected"
        );
        await Reject(
            () => scenes.SaveFogAsync(1, scene.Id, scene.Revision, 50, []),
            "Stale grid rejected"
        );
        Check((await scenes.LoadFogAsync(1, scene.Id)).Count == 2, "Invalid saves preserve fog");
        await Reject(
            () => scenes.SetFogEnabledAsync(2, scene.Id, false),
            "Fog setting respects campaigns"
        );
        await Reject(
            async () =>
            {
                await scenes.FogEnabledAsync(2, scene.Id);
            },
            "Fog setting reads respect campaigns"
        );
        await scenes.SetFogEnabledAsync(1, scene.Id, false);
        Check(!await scenes.FogEnabledAsync(1, scene.Id), "Fog can be turned off");
        Check(
            (await scenes.LoadFogAsync(1, scene.Id)).Count == 2,
            "Turning fog off keeps the painted fog"
        );
        await scenes.SetFogEnabledAsync(1, scene.Id, true);
        await scenes.SaveGridAsync(1, scene.Id, "#ffffff", 1, 20, 100);
        Check((await scenes.LoadFogAsync(1, scene.Id)).Count == 2, "Grid styling preserves fog");
        await scenes.SaveGridAsync(1, scene.Id, "#ffffff", 1, 20, 50);
        Check((await scenes.LoadFogAsync(1, scene.Id)).Count == 0, "Grid size changes reset fog");
        await scenes.SaveFogAsync(1, scene.Id, scene.Revision, 50, [new(0, 0)]);
        await scenes.SaveAsync(
            new SceneInput
            {
                Id = scene.Id,
                CampaignId = 1,
                Name = "Fog",
                GridSize = 50,
            },
            SceneImageChecks.Png(300, 200)
        );
        Check(
            (await scenes.LoadFogAsync(1, scene.Id)).Count == 0,
            "Replacement backgrounds reset fog"
        );
        Check(await scenes.FogEnabledAsync(1, scene.Id), "Scene edits keep the fog setting");
        Console.WriteLine(
            "Fog migration, covered defaults, on/off setting, persistence, bounds, campaign isolation, stale saves and grid/image resets passed."
        );
    }
}
