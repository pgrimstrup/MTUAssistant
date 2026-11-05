using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class SpeedChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        var actor = new SceneCombatant(
            1,
            1,
            "Fast traveller",
            false,
            false,
            true,
            0,
            0,
            "777777",
            Speed: 9
        );
        var foe = new SceneCombatant(2, 2, "Creature", true, false, true, 1, 0, "777777", Speed: 4);
        var roster = new[] { actor, foe };
        Encounter Fresh()
        {
            var e = new Encounter();
            e.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
            return e;
        }
        foreach (var sample in new[] { ("Standing", 19.5), ("Crouching", 10.5), ("Prone", 6.0) })
        {
            var e = Fresh();
            e.ChooseAction(1, "Change Stance", roster);
            e.SetStance(1, sample.Item1, roster);
            e.ChooseAction(1, "Move", roster);
            Check(
                e.ActionFor(1).MovementDiameter == sample.Item2,
                "Speed 9 Move diameter in " + sample.Item1
            );
        }
        var moves = Fresh();
        moves.ChooseAction(1, "Move", roster);
        moves.ChooseAction(1, "Move", roster);
        Check(
            moves.ActionFor(1).MovementDiameter == 37.5,
            "Two Move actions use twice the character Speed"
        );
        moves.ChooseAction(1, "Move", roster);
        Check(
            moves.ActionFor(1).MovementDiameter == 55.5,
            "Three Move actions use three times Speed"
        );
        moves.ChooseAction(2, "Move", roster);
        Check(
            moves.ActionFor(2).MovementDiameter == 9.5,
            "NPC Speed is independent of Traveller Speed"
        );
        var restored = new Encounter();
        restored.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(moves.Snapshot())
            )!,
            roster
        );
        Check(
            restored.ActionFor(1).MovementDiameter == 55.5,
            "Speed-based movement survives save/restore"
        );
        var fasterRoster = new[] { actor with { Speed = 12 }, foe };
        Check(
            restored.MovementRanges(fasterRoster).Single(r => r.Id == 1).Diameter == 73.5,
            "Existing movement circle updates when character Speed changes"
        );
        restored.ResetActions(1, fasterRoster);
        restored.ChooseAction(1, "Move", fasterRoster);
        Check(restored.ActionFor(1).MovementDiameter == 25.5, "Reset preserves current Speed");
        var zeroRoster = new[] { actor with { Speed = 0 }, foe };
        restored.ResetActions(1, zeroRoster);
        restored.ChooseAction(1, "Move", zeroRoster);
        Check(
            restored.ActionFor(1).MovementDiameter == 1.5,
            "Zero Speed only covers the occupied grid cell"
        );
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(connection).Options;
        using var db = new MtuDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20261105034204_InjuryTolerance");
        db.Campaigns.Add(new CampaignEntity { Id = 1, Name = "Speed" });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Characters (Id, CampaignId, Name, PlayerName, Race, Upp, TermsServed, HighestNavalRank, HighestScoutRank, NaturalDr, Inactive, IsNpc) VALUES (1,1,'Existing traveller','','','777777','0',0,0,0,0,0),(2,1,'Existing NPC','','','777777','0',0,0,0,0,1)"
        );
        await db.Database.MigrateAsync();
        Check(
            await db.Characters.AllAsync(c => c.Speed == 6),
            "Migration defaults every existing Traveller and NPC to Speed 6"
        );
        var characters = new CharacterService(db);
        var npcs = new NpcService(db, characters);
        var traveller = (await characters.ListAsync(1)).Single();
        await characters.SaveAsync(traveller with { Speed = 9 });
        await npcs.SaveAsync(
            new Npc(new Character(0, 1, "", "Speed creature", "", "777777", "0", 0, 0, 0, Speed: 4))
        );
        var npc = (await npcs.ListAsync(1)).Single();
        Check(
            (await characters.ListAsync(1)).Single().Speed == 9 && npc.Character.Speed == 4,
            "Traveller/NPC services persist editable integer Speed"
        );
        var services = new ServiceCollection();
        services.AddDbContext<MtuDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<SceneService>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var scenes = scope.ServiceProvider.GetRequiredService<SceneService>();
        var scene = await scenes.SaveAsync(
            new SceneInput { CampaignId = 1, Name = "Speed scene" },
            SceneImageChecks.Png(16, 16)
        );
        await scenes.MoveTravellersHereAsync(1, scene.Id);
        var instance = await scenes.AddNpcInstanceAsync(1, scene.Id, npc.Character.Id);
        Check(instance.Speed == 4, "New NPC scene instance carries Speed");
        Check(
            (await scenes.PositionCombatantAsync(1, scene.Id, instance.Id, 2, 2)).Speed == 4,
            "Moving an NPC preserves Speed"
        );
        var entries = await scenes.CombatantsAsync(1, scene.Id);
        Check(
            entries.Single(e => e.CharacterId == traveller.Id).Speed == 9
                && entries.Single(e => e.Id == instance.Id).Speed == 4,
            "Scene combatants load each character Speed"
        );
        Console.WriteLine(
            "Character Speed migration, Traveller/NPC persistence, scene instances, stance-scaled Move circles and restore passed."
        );
    }
}
