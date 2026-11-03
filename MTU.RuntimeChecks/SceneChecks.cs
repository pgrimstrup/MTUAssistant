using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class SceneChecks
{
    public static async Task RunAsync()
    {
        SceneImageChecks.Run();
        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
        static async Task Reject(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new Exception("Invalid scene operation accepted");
        }
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<MtuDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<SceneService>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103071653_NpcType");
        db.Campaigns.AddRange(
            new CampaignEntity { Id = 1, Name = "Scenes" },
            new CampaignEntity { Id = 2, Name = "Other" }
        );
        await db.SaveChangesAsync();
        await migrator.MigrateAsync("20261103095749_Scenes");
        db.Scenes.Add(
            new SceneEntity
            {
                CampaignId = 1,
                Name = "Existing scene",
                Image = [1],
            }
        );
        await db.SaveChangesAsync();
        await migrator.MigrateAsync("20261103103811_SceneTokens");
        var legacyScene = await db.Scenes.SingleAsync(s => s.Name == "Existing scene");
        db.Characters.AddRange(
            new CharacterEntity
            {
                Id = 301,
                CampaignId = 1,
                Name = "Legacy Traveller",
            },
            new CharacterEntity
            {
                Id = 302,
                CampaignId = 1,
                Name = "Legacy Mercenary",
                IsNpc = true,
            }
        );
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO SceneTokens (SceneId, CharacterId, \"Column\", \"Row\") VALUES ({legacyScene.Id}, {301}, {2}, {3}), ({legacyScene.Id}, {302}, {4}, {5})"
        );
        await migrator.MigrateAsync();
        Check(await db.Campaigns.CountAsync() == 2, "Scene migration preserves campaigns");
        Check(
            await db.Scenes.AnyAsync(s => s.Name == "Existing scene"),
            "Token migration preserves existing scenes"
        );
        var store = scope.ServiceProvider.GetRequiredService<SceneService>();
        var png = SceneImageChecks.Png(16, 16);
        var input = new SceneInput { CampaignId = 1, Name = "Hangar" };
        var saved = await store.SaveAsync(input, png);
        Check(
            saved.GridSize == 100
                && saved.GridColour == "#000000"
                && saved.GridOpacity == 50
                && saved.GridlineWidth == 2,
            "Scene defaults are 100px, black, 50 percent and 2px"
        );
        Check(
            (await store.ImageAsync(1, saved.Id))!.Bytes.SequenceEqual(png),
            "Background image persists"
        );
        Check(
            !(await store.ListAsync(2)).Any()
                && await store.FindAsync(2, saved.Id) is null
                && await store.ImageAsync(2, saved.Id) is null,
            "Scenes and images are campaign scoped"
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    Id = saved.Id,
                    CampaignId = 2,
                    Name = "Wrong campaign",
                }
            )
        );
        await Reject(() => store.DeleteAsync(2, saved.Id));
        var updated = await store.SaveAsync(
            new SceneInput
            {
                Id = saved.Id,
                CampaignId = 1,
                Name = "Deck",
                GridSize = 75,
                GridColour = "#AABBCC",
                GridOpacity = 0,
                GridlineWidth = 1.5,
            }
        );
        Check(
            updated.Name == "Deck"
                && updated.GridSize == 75
                && updated.GridColour == "#AABBCC"
                && updated.GridOpacity == 0
                && updated.GridlineWidth == 1.5
                && updated.Revision > saved.Revision
                && (await store.ImageAsync(1, saved.Id))!.Bytes.SequenceEqual(png),
            "Editing grid settings retains image and advances revision"
        );
        var replacement = Convert.FromBase64String(
            "R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"
        );
        await store.SaveAsync(
            new SceneInput
            {
                Id = saved.Id,
                CampaignId = 1,
                Name = "Deck",
            },
            replacement
        );
        Check(
            (await store.ImageAsync(1, saved.Id)) is { ContentType: "image/gif" },
            "Image replacement updates its content type"
        );
        await Reject(() => store.SaveAsync(new SceneInput { CampaignId = 1, Name = "" }, png));
        await Reject(() => store.SaveAsync(new SceneInput { CampaignId = 1, Name = "No image" }));
        await Reject(() =>
            store.SaveAsync(new SceneInput { CampaignId = 1, Name = "Invalid image" }, [1, 2, 3])
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Grid",
                    GridSize = 0,
                },
                png
            )
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Grid",
                    GridOpacity = 101,
                },
                png
            )
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Grid",
                    GridColour = "red",
                },
                png
            )
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Grid",
                    GridlineWidth = double.NaN,
                },
                png
            )
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput
                {
                    CampaignId = 1,
                    Name = "Grid",
                    GridlineWidth = 101,
                },
                png
            )
        );
        var maximum = SceneImageChecks.Png(
            3000,
            2000,
            System.IO.Compression.CompressionLevel.NoCompression
        );
        var large = await store.SaveAsync(
            new SceneInput { CampaignId = 1, Name = "Large" },
            maximum
        );
        Check(
            large.ImageBytes > 20 * 1024 * 1024,
            "Valid images larger than the former 20 MB limit are accepted"
        );
        var square = await store.SaveAsync(
            new SceneInput { CampaignId = 1, Name = "Maximum dimensions" },
            SceneImageChecks.Png(4096, 4096)
        );
        Check(square.ImageBytes > 0, "4096 by 4096 images can be saved");
        await store.DeleteAsync(1, square.Id);
        await Reject(() =>
            store.SaveAsync(
                new SceneInput { CampaignId = 1, Name = "Too wide" },
                SceneImageChecks.Png(4097, 1)
            )
        );
        await Reject(() =>
            store.SaveAsync(
                new SceneInput { CampaignId = 1, Name = "Too tall" },
                SceneImageChecks.Png(1, 4097)
            )
        );
        var legacyEntries = await store.CombatantsAsync(1, legacyScene.Id);
        Check(
            legacyEntries.Count == 2
                && legacyEntries.Select(t => t.Id).Distinct().Count() == 2
                && legacyEntries.Single(t => t.CharacterId == 302)
                    is { Name: "Legacy Mercenary 1", Column: 4, Row: 5, IsVisible: true }
                && legacyEntries.Single(t => t.CharacterId == 301) is { Column: 2, Row: 3 },
            "Migration preserves placements and allocates independent instance IDs"
        );
        db.Characters.AddRange(
            new CharacterEntity
            {
                Id = 101,
                CampaignId = 1,
                Name = "Traveller",
                TokenImage = png,
            },
            new CharacterEntity
            {
                Id = 102,
                CampaignId = 1,
                Name = "Mercenary",
                IsNpc = true,
            },
            new CharacterEntity
            {
                Id = 103,
                CampaignId = 2,
                Name = "Other campaign",
                IsNpc = true,
            },
            new CharacterEntity
            {
                Id = 104,
                CampaignId = 1,
                Name = "Inactive",
                Inactive = true,
            }
        );
        await db.SaveChangesAsync();
        var choices = await store.NpcChoicesAsync(1);
        Check(
            choices.Any(c => c.CharacterId == 102)
                && !choices.Any(c => c.CharacterId is 101 or 103 or 104),
            "NPC search choices are campaign scoped and exclude Travellers"
        );
        Check(
            (await store.TokenImageAsync(1, 101))!.Bytes.SequenceEqual(png)
                && await store.TokenImageAsync(2, 101) is null,
            "Token images remain campaign scoped"
        );
        await store.SynchronizeTravellersAsync(1, saved.Id);
        await store.SynchronizeTravellersAsync(1, saved.Id);
        var roster = await store.CombatantsAsync(1, saved.Id);
        Check(
            roster.Count == 2
                && roster.All(t => !t.IsNpc && !t.OnMap)
                && !roster.Any(t => t.CharacterId == 104),
            "Active Travellers are added once, without placing tokens or including inactive players"
        );
        var traveller = roster.Single(t => t.CharacterId == 101);
        var squad = new List<SceneCombatant>();
        for (var i = 0; i < 4; i++)
            squad.Add(await store.AddNpcInstanceAsync(1, saved.Id, 102));
        Check(
            squad.Select(t => t.Id).Distinct().Count() == 4
                && squad
                    .Select(t => t.Name)
                    .SequenceEqual(
                        new[] { "Mercenary 1", "Mercenary 2", "Mercenary 3", "Mercenary 4" }
                    ),
            "Repeated NPCs have unique identities and numbered names"
        );
        await store.RenameNpcInstanceAsync(1, saved.Id, squad[1].Id, "Squad leader");
        Check(
            (await db.Characters.AsNoTracking().SingleAsync(c => c.Id == 102)).Name == "Mercenary",
            "Instance rename does not change the source NPC"
        );
        await store.PositionCombatantAsync(1, saved.Id, squad[0].Id, 2, 3);
        await store.PositionCombatantAsync(1, saved.Id, squad[1].Id, 4, 5);
        await store.SetCombatantVisibilityAsync(1, saved.Id, squad[0].Id, false);
        await store.PositionCombatantAsync(1, saved.Id, squad[0].Id, 6, 7);
        roster = await store.CombatantsAsync(1, saved.Id);
        Check(
            roster.Take(2).All(t => !t.IsNpc) && roster.Skip(2).All(t => t.IsNpc),
            "Travellers sort before NPC instances"
        );
        Check(
            roster.Single(t => t.Id == squad[0].Id) is { Column: 6, Row: 7, IsVisible: false }
                && roster.Single(t => t.Id == squad[1].Id)
                    is { Name: "Squad leader", Column: 4, Row: 5, IsVisible: true },
            "Instance names, moves and visibility persist independently"
        );
        await store.PositionCombatantAsync(1, saved.Id, squad[1].Id, null, null);
        Check(
            (await store.CombatantsAsync(1, saved.Id)).Single(t => t.Id == squad[1].Id)
                is { OnMap: false, Name: "Squad leader" },
            "Unplacing retains the instance in the tracker"
        );
        Check(
            (await store.AddNpcInstanceAsync(1, saved.Id, 102)).Name == "Mercenary 5",
            "Numbering is independent of edited names"
        );
        await Reject(() => store.AddNpcInstanceAsync(1, saved.Id, 101));
        await Reject(() => store.AddNpcInstanceAsync(1, saved.Id, 103));
        await Reject(() => store.SynchronizeTravellersAsync(2, saved.Id));
        await Reject(() => store.PositionCombatantAsync(2, saved.Id, squad[0].Id, 0, 0));
        await Reject(() => store.PositionCombatantAsync(1, large.Id, squad[0].Id, 0, 0));
        await Reject(() => store.PositionCombatantAsync(1, saved.Id, squad[0].Id, -1, 0));
        await Reject(() => store.PositionCombatantAsync(1, saved.Id, squad[0].Id, null, 0));
        await Reject(() => store.SetCombatantVisibilityAsync(2, saved.Id, squad[0].Id, true));
        await Reject(() => store.RenameNpcInstanceAsync(2, saved.Id, squad[0].Id, "Wrong"));
        await Reject(() => store.RenameNpcInstanceAsync(1, saved.Id, squad[0].Id, " "));
        await Reject(() => store.RenameNpcInstanceAsync(1, saved.Id, traveller.Id, "Not an NPC"));
        await Reject(() => store.RemoveNpcInstanceAsync(1, saved.Id, traveller.Id));
        await Reject(() => store.RemoveNpcInstanceAsync(2, saved.Id, squad[0].Id));
        Check(!(await store.CombatantsAsync(2, saved.Id)).Any(), "Rosters are campaign scoped");
        var beforeGrid = (await store.FindAsync(1, saved.Id))!;
        var liveGrid = await store.SaveGridAsync(1, saved.Id, "#ffffff", 3.5, 30);
        Check(
            liveGrid.Revision == beforeGrid.Revision
                && liveGrid.GridlineWidth == 3.5
                && liveGrid.GridOpacity == 30
                && liveGrid.GridColour == "#ffffff"
                && (await store.ImageAsync(1, saved.Id))!.Bytes.SequenceEqual(replacement),
            "Live grid edits persist without resetting camera revision or replacing the image"
        );
        await Reject(() => store.SaveGridAsync(2, saved.Id, "#ffffff", 2, 30));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "red", 2, 30));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", 0, 30));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", double.NaN, 30));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", 2, 101));
        var resizedGrid = await store.SaveGridAsync(1, saved.Id, "#ffffff", 2, 30, gridSize: 10);
        Check(
            resizedGrid.GridSize == 10
                && resizedGrid.Revision == beforeGrid.Revision
                && (await store.FindAsync(1, saved.Id))!.GridSize == 10
                && (await store.CombatantsAsync(1, saved.Id)).Single(t => t.Id == squad[0].Id)
                    is { Column: 6, Row: 7 },
            "Toolbar grid size persists without changing token cells or camera revision"
        );
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", 2, 30, gridSize: 0));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", 2, 30, gridSize: -1));
        await Reject(() => store.SaveGridAsync(1, saved.Id, "#ffffff", 3, 30, gridSize: 2));
        await store.RemoveNpcInstanceAsync(1, saved.Id, squad[0].Id);
        Check(
            (await store.CombatantsAsync(1, saved.Id)).Count == 6,
            "Removing an instance leaves other instances and Travellers intact"
        );
        var player = await db.Characters.SingleAsync(c => c.Id == 101);
        player.Inactive = true;
        await db.SaveChangesAsync();
        await store.SynchronizeTravellersAsync(1, saved.Id);
        Check(
            !(await store.CombatantsAsync(1, saved.Id)).Any(t => t.CharacterId == 101),
            "Newly inactive players are removed from the encounter"
        );
        player.Inactive = false;
        await db.SaveChangesAsync();
        await store.SynchronizeTravellersAsync(1, saved.Id);
        Check(
            (await store.CombatantsAsync(1, saved.Id)).Count(t => t.CharacterId == 101) == 1,
            "Reactivated players are restored once"
        );
        db.Characters.Remove(await db.Characters.SingleAsync(c => c.Id == 102));
        await db.SaveChangesAsync();
        Check(
            !(await store.CombatantsAsync(1, saved.Id)).Any(t => t.CharacterId == 102),
            "Character deletion removes all instances"
        );
        await store.SynchronizeTravellersAsync(1, large.Id);
        var largePlayer = (await store.CombatantsAsync(1, large.Id)).First();
        await store.PositionCombatantAsync(1, large.Id, largePlayer.Id, 0, 0);
        await store.DeleteAsync(1, large.Id);
        Check(
            !(await db.SceneTokens.AnyAsync(t => t.SceneId == large.Id)),
            "Scene deletion cascades tokens"
        );

        Check(
            await store.FindAsync(1, large.Id) is null
                && await store.ImageAsync(1, large.Id) is null,
            "Delete removes the scene and background"
        );
        db.Campaigns.Remove(await db.Campaigns.SingleAsync(c => c.Id == 1));
        await db.SaveChangesAsync();
        Check(!(await store.ListAsync(1)).Any(), "Campaign deletion cascades scenes");
        Check(!await db.SceneTokens.AnyAsync(), "Campaign deletion cascades tokens");
        Console.WriteLine(
            "Combat Tracker migration, instances, active Travellers, visibility, placement, live grid, image limits, cascades and campaign isolation checks passed."
        );
    }
}
