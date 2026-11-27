using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class CharacterSheetChecks
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

        // The current value shown on a characteristic card.
        var none = CurrentCharacteristics.None;
        Check(
            none.Value(0, 7) == 7 && none.Value(2, 9) == 9,
            "With no damage recorded, STR, DEX and END are at their scores"
        );
        Check(
            none.Value(3, 8) is null && none.Value(0, null) is null,
            "INT, EDU, SOC and unknown scores have no current value"
        );
        var hurt = new CurrentCharacteristics(1, new CombatHealth(5, 9, 0));
        Check(
            hurt.Value(0, 7) == 5 && hurt.Value(1, 9) == 9 && hurt.Value(2, 8) == 0,
            "Recorded damage gives the current STR, DEX and END"
        );
        Check(
            new CurrentCharacteristics(2, null).Value(0, 7) is null,
            "A character on the map more than once has no single current value"
        );

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
        var characters = new CharacterService(db);
        var npcs = new NpcService(db, characters);
        var scenes = scope.ServiceProvider.GetRequiredService<SceneService>();

        // Record counts for the tab badges.
        var scout = await characters.SaveAsync(
            new Character(0, 1, "Player", "Scout", "", "7A8B96", 0)
        );
        Check(
            await characters.RecordCountsAsync(1, scout) == new CharacterRecordCounts(0, 0, 0, 0),
            "A new Traveller has no records"
        );
        var vest = new EquipmentItem { Kind = EquipmentKind.Armour, Name = "Sheet check vest" };
        var pistol = new EquipmentItem { Kind = EquipmentKind.Weapon, Name = "Sheet check pistol" };
        var knife = new EquipmentItem { Kind = EquipmentKind.Weapon, Name = "Sheet check knife" };
        db.Equipment.AddRange(vest, pistol, knife);
        await db.SaveChangesAsync();
        db.CareerTerms.AddRange(
            new CareerTermEntity
            {
                CharacterId = scout,
                Sequence = 1,
                Service = "Scouts",
            },
            new CareerTermEntity
            {
                CharacterId = scout,
                Sequence = 2,
                Service = "Scouts",
            }
        );
        db.CharacterSkills.AddRange(
            new CharacterSkillEntity
            {
                CharacterId = scout,
                Name = "Pilot (Small Craft)",
                Level = 1,
            },
            new CharacterSkillEntity
            {
                CharacterId = scout,
                Name = "Recon",
                Level = 1,
            },
            new CharacterSkillEntity
            {
                CharacterId = scout,
                Name = "Survival",
                Level = 0,
            }
        );
        db.CharacterEquipment.AddRange(
            new CharacterEquipmentEntity { CharacterId = scout, EquipmentId = vest.Id },
            new CharacterEquipmentEntity { CharacterId = scout, EquipmentId = pistol.Id },
            new CharacterEquipmentEntity { CharacterId = scout, EquipmentId = knife.Id }
        );
        await db.SaveChangesAsync();
        Check(
            await characters.RecordCountsAsync(1, scout) == new CharacterRecordCounts(2, 3, 1, 2),
            "Career terms, skills, armour and weapons are counted"
        );
        Check(
            await characters.RecordCountsAsync(2, scout) == new CharacterRecordCounts(0, 0, 0, 0),
            "Counting another campaign's character gives nothing"
        );

        // Current STR, DEX and END from the Combat Tracker.
        var scene = await scenes.SaveAsync(
            new SceneInput
            {
                CampaignId = 1,
                Name = "Sheet",
                GridSize = 100,
            },
            SceneImageChecks.Png(200, 200)
        );
        Check(
            await scenes.CurrentCharacteristicsAsync(1, scout) == CurrentCharacteristics.None,
            "A Traveller who is in no scene has no recorded damage"
        );
        await scenes.MoveTravellersHereAsync(1, scene.Id);
        var token = (await scenes.CombatantsAsync(1, scene.Id)).Single(c => c.CharacterId == scout);
        Check(
            await scenes.CurrentCharacteristicsAsync(1, scout) is { Instances: 1, Health: null },
            "A Traveller who has not been hurt is at full"
        );
        async Task SetState(EncounterSnapshot state)
        {
            var json = JsonSerializer.Serialize(state);
            await db
                .Scenes.Where(s => s.Id == scene.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.EncounterState, json));
        }
        await SetState(
            new EncounterSnapshot(
                false,
                0,
                null,
                0,
                [],
                null,
                [],
                new Dictionary<int, CombatHealth> { [token.Id] = new(7, 4, 0) }
            )
        );
        var current = await scenes.CurrentCharacteristicsAsync(1, scout);
        Check(
            current.Value(0, 7) == 7 && current.Value(1, 10) == 4 && current.Value(2, 8) == 0,
            "Damage saved by the Combat Tracker gives the current values"
        );
        Check(
            await scenes.CurrentCharacteristicsAsync(2, scout) == CurrentCharacteristics.None,
            "Current values respect campaigns"
        );
        // An older scene with only a hit point total: END goes first, as in the Combat Tracker.
        await SetState(new EncounterSnapshot(false, 0, null, 0, [], new() { [token.Id] = 20 }, []));
        current = await scenes.CurrentCharacteristicsAsync(1, scout);
        Check(
            current.Value(0, 7) == 7 && current.Value(1, 10) == 10 && current.Value(2, 8) == 3,
            "A stored hit point total is spread over the characteristics"
        );

        // Patrons and Enemies: Natural DR, and no single value when placed more than once.
        var guardId = await npcs.SaveAsync(
            new Npc(new Character(0, 1, "", "Guard", "", "888777", 2), Type: NpcType.Combatant)
        );
        Check(
            (await npcs.ListAsync(1)).Single(n => n.Character.Id == guardId).Character.NaturalDr
                == 2,
            "An NPC's Natural DR is saved"
        );
        var guard = (await npcs.ListAsync(1)).Single(n => n.Character.Id == guardId);
        await Reject(
            () =>
                npcs.SaveAsync(guard with { Character = guard.Character with { NaturalDr = -1 } }),
            "A negative NPC Natural DR is rejected"
        );
        await scenes.AddNpcInstanceAsync(1, scene.Id, guardId);
        Check(
            await scenes.CurrentCharacteristicsAsync(1, guardId) is { Instances: 1 },
            "One NPC instance has its own current values"
        );
        await scenes.AddNpcInstanceAsync(1, scene.Id, guardId);
        Check(
            await scenes.CurrentCharacteristicsAsync(1, guardId) is { Instances: 2, Health: null },
            "Several NPC instances have no single current value"
        );
        Console.WriteLine(
            "Character sheet record counts, current characteristics, NPC Natural DR and campaign isolation passed."
        );
    }
}
