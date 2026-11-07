using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class InstanceNameChecks
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
        await db.GetService<IMigrator>().MigrateAsync("20261107020000_SceneFog");
        db.Campaigns.Add(new CampaignEntity { Id = 1, Name = "Test" });
        await db.SaveChangesAsync();
        var guard = new CharacterEntity
        {
            CampaignId = 1,
            Name = "Guard",
            IsNpc = true,
            Upp = "777777",
        };
        var beast = new CharacterEntity
        {
            CampaignId = 1,
            Name = "Beast",
            IsNpc = true,
            IsAnimal = true,
        };
        db.Characters.AddRange(guard, beast);
        await db.SaveChangesAsync();
        db.Animals.Add(new AnimalEntity { CharacterId = beast.Id, Hits = 10 });
        await db.SaveChangesAsync();
        var store = scope.ServiceProvider.GetRequiredService<SceneService>();
        // Insert with SQL: the database is still at the SceneFog schema, so the current model can't save scenes.
        async Task<int> LegacySceneAsync(string name)
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO Scenes (CampaignId, Name, Image, ContentType, GridSize, GridColour, GridOpacity, GridlineWidth, Revision) VALUES (1, {name}, {SceneImageChecks.Png(500, 500)}, {"image/png"}, 100, {"#000000"}, 50, 2, 1)"
            );
            return await db
                .Database.SqlQuery<int>($"SELECT Id AS Value FROM Scenes WHERE Name = {name}")
                .SingleAsync();
        }
        var scene = new { Id = await LegacySceneAsync("Names") };
        var otherScene = new { Id = await LegacySceneAsync("Other") };
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO SceneTokens (SceneId, CharacterId, Name, InstanceNumber, IsVisible, \"Column\", \"Row\") VALUES ({scene.Id}, {guard.Id}, {"Guard 1"}, 1, 1, 0, 0)"
        );
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO SceneTokens (SceneId, CharacterId, Name, InstanceNumber, IsVisible, \"Column\", \"Row\") VALUES ({otherScene.Id}, {guard.Id}, {"Guard 42"}, 1, 1, 0, 0)"
        );
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO SceneTokens (SceneId, CharacterId, Name, InstanceNumber, IsVisible) VALUES ({scene.Id}, {guard.Id}, {"Guard 2"}, 2, 1)"
        );
        await db.Database.MigrateAsync();
        var legacyAuto = (await store.CombatantsAsync(1, scene.Id)).OrderBy(e => e.Id).ToArray();
        var legacyCustom = (await store.CombatantsAsync(1, otherScene.Id)).Single();
        Check(
            legacyAuto.Select(e => e.Name).SequenceEqual(new[] { "Guard 1", "Guard 2" })
                && legacyCustom.Name == "Guard 42",
            "Migration numbers placed and unplaced tracker entries and preserves custom names"
        );
        foreach (var legacy in legacyAuto)
            await store.RemoveNpcInstanceAsync(1, scene.Id, legacy.Id);
        await store.RemoveNpcInstanceAsync(1, otherScene.Id, legacyCustom.Id);
        foreach (var source in new[] { guard, beast })
        {
            async Task<string> Name(int id) =>
                (await store.CombatantsAsync(1, scene.Id)).Single(e => e.Id == id).Name;
            var first = await store.AddNpcInstanceAsync(1, scene.Id, source.Id);
            Check(first.Name == source.Name, "First tracker instance has no suffix");
            var second = await store.AddNpcInstanceAsync(1, scene.Id, source.Id);
            Check(
                await Name(first.Id) == source.Name + " 1" && second.Name == source.Name + " 2",
                "Second tracker instance numbers both before placement"
            );
            await store.PositionCombatantAsync(1, scene.Id, second.Id, 0, 0);
            await store.PositionCombatantAsync(1, scene.Id, first.Id, 1, 0);
            Check(
                await Name(first.Id) == source.Name + " 1"
                    && await Name(second.Id) == source.Name + " 2",
                "Drop order does not change tracker numbers"
            );
            await store.PositionCombatantAsync(1, scene.Id, first.Id, null, null);
            Check(
                await Name(second.Id) == source.Name + " 2",
                "Unplacement retains tracker numbering"
            );
            var third = await store.AddNpcInstanceAsync(1, scene.Id, source.Id);
            Check(third.Name == source.Name + " 3", "Later tracker additions continue numbering");
            await store.RemoveNpcInstanceAsync(1, scene.Id, third.Id);
            await store.RemoveNpcInstanceAsync(1, scene.Id, first.Id);
            Check(
                await Name(second.Id) == source.Name,
                "Removing tracker entries down to one strips the suffix"
            );
            await store.RenameNpcInstanceAsync(1, scene.Id, second.Id, source.Name + " 42");
            var another = await store.AddNpcInstanceAsync(1, scene.Id, source.Id);
            Check(
                another.Name == source.Name + " 2",
                "Custom numeric names do not affect auto numbering"
            );
            await store.RemoveNpcInstanceAsync(1, scene.Id, another.Id);
            Check(
                await Name(second.Id) == source.Name + " 42",
                "Custom names survive tracker additions and removals"
            );
        }
        var roster = await store.CombatantsAsync(1, scene.Id);
        var actor = roster.Single(e => e.CharacterId == guard.Id);
        var target = roster.Single(e => e.CharacterId == beast.Id);
        var dirty = new CombatantActionState(
            MajorAction: "Ranged Attack",
            TargetId: target.Id,
            Stance: "Prone",
            PendingStance: "Crouching",
            BehindCover: true,
            PendingBehindCover: true,
            Hiding: true,
            PendingHiding: true,
            Elevated: true,
            PendingElevated: true,
            Flying: true,
            PendingFlying: true,
            Unconscious: true,
            TargetTagged: true,
            TargetHasBeenHit: true,
            CarriedAimBonus: 4,
            AutoAttack: "Full Auto",
            AutoScore: 3,
            ExtraTargets: [new(target.Id)],
            CoverProtection: new(1, "Wall", 2, 3)
        );
        var health = new Dictionary<int, CombatHealth>
        {
            [actor.Id] = new(5, 6, 7),
            [target.Id] = new(0, 0, 3),
        };
        var original = new EncounterSnapshot(
            true,
            1,
            actor.Id,
            0,
            new() { [actor.Id] = 20, [target.Id] = 19 },
            null,
            [],
            Attributes: health,
            Actions: new()
            {
                [actor.Id] = dirty,
                [target.Id] = new(TargetId: actor.Id, Stance: "Prone", Hiding: true),
            }
        );
        await store.SaveEncounterAsync(1, scene.Id, original);
        await store.PositionCombatantAsync(1, scene.Id, actor.Id, 1, 0);
        await store.PositionCombatantAsync(1, scene.Id, actor.Id, null, null);
        var cleared = (await store.LoadEncounterAsync(1, scene.Id))!;
        var clean = cleared.Actions![actor.Id];
        Check(
            clean.TargetIds.Count == 0
                && clean.SelectedStance == "Standing"
                && !clean.SelectedBehindCover
                && !clean.SelectedHiding
                && !clean.SelectedElevated
                && !clean.SelectedFlying
                && !clean.Unconscious
                && !clean.TargetTagged
                && !clean.TargetHasBeenHit
                && clean.SelectedCoverProtection is null
                && clean.CarriedAimBonus == 0,
            "Unplacement clears primary/extra targets, current/pending states and stance"
        );
        Check(
            cleared.Attributes![actor.Id] == health[actor.Id]
                && cleared.Attributes[target.Id] == health[target.Id],
            "Unplacement preserves all Hits taken"
        );
        Check(
            cleared.Actions[target.Id].TargetId is null && cleared.Actions[target.Id].Hiding,
            "Targets pointing to the removed token clear without changing another actor's states"
        );
        Check(
            clean.MajorAction == "Ranged Attack",
            "Unplacement preserves selected action choices"
        );
        await store.PositionCombatantAsync(1, scene.Id, actor.Id, 2, 0);
        var reloaded = (await store.LoadEncounterAsync(1, scene.Id))!;
        Check(
            System.Text.Json.JsonSerializer.Serialize(reloaded.Actions![actor.Id])
                == System.Text.Json.JsonSerializer.Serialize(clean)
                && reloaded.Attributes![actor.Id] == health[actor.Id],
            "Replacement retains cleared states and existing damage"
        );
        Console.WriteLine(
            "Tracker numbering, custom names, map-state clearing, incoming targets, persistence and unchanged Hits passed."
        );
    }
}
