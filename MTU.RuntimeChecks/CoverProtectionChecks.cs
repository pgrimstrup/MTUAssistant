using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MTU.Core;
using MTU.Core.Data;

public static class CoverProtectionChecks
{
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    public static async Task RunAsync()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var db = new MtuDbContext(
            new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(connection).Options
        );
        await db.Database.MigrateAsync();
        var store = new CoverProtectionService(db);
        var seeds = await store.ListAsync();
        Check(
            seeds.Count == 21
                && seeds.Single(c => c.Name == "Steel Blast Door") is { Protection: 12, Hits: 150 }
                && seeds.Single(c => c.Name == "Personal Computer") is { Protection: 0, Hits: 6 },
            "Cover migration seeds reference values"
        );
        var custom = new CoverProtection
        {
            Name = " Test cover ",
            Protection = 5,
            Hits = 10,
        };
        await store.SaveAsync(custom);
        custom.Protection = 7;
        await store.SaveAsync(custom);
        Check(
            (await store.ListAsync()).Single(c => c.Id == custom.Id)
                is { Name: "Test cover", Protection: 7 },
            "Cover add/edit"
        );
        foreach (
            var invalid in new[]
            {
                new CoverProtection
                {
                    Name = "test COVER",
                    Protection = 1,
                    Hits = 1,
                },
                new CoverProtection
                {
                    Name = "",
                    Protection = 1,
                    Hits = 1,
                },
                new CoverProtection
                {
                    Name = "Negative",
                    Protection = -1,
                    Hits = 1,
                },
                new CoverProtection
                {
                    Name = "Zero Hits",
                    Protection = 1,
                    Hits = 0,
                },
            }
        )
        {
            try
            {
                await store.SaveAsync(invalid);
                throw new Exception("Invalid cover accepted");
            }
            catch (ArgumentException) { }
        }
        var hero = new SceneCombatant(1, 1, "Hero", false, false, true, 0, 0, "777777");
        var foe = new SceneCombatant(2, 2, "Foe", true, false, true, 2, 0, "777777");
        var roster = new[] { hero, foe };
        var encounter = new Encounter();
        encounter.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
        encounter.SetCoverStatus(1, true, true, roster);
        var cover = new CombatCover(custom.Id, "Test cover", 7, 10);
        encounter.SetCoverProtection(1, cover, roster);
        Check(
            encounter.ActionFor(1).EffectiveCoverProtection == 7
                && !encounter.CanChooseAction(1, "Ranged Attack"),
            "Hiding protects and blocks engagement"
        );
        encounter.ResetActions(1, roster);
        Check(
            encounter.ActionFor(1).SelectedCoverProtection is null,
            "Reset cancels pending cover"
        );
        encounter.SetCoverStatus(1, true, true, roster);
        encounter.SetCoverProtection(1, cover, roster);
        encounter.ResolveActions(1, roster);
        var saved = JsonSerializer.Deserialize<EncounterSnapshot>(
            JsonSerializer.Serialize(encounter.Snapshot())
        )!;
        var restored = new Encounter();
        restored.Restore(saved, roster);
        Check(
            restored.ActionFor(1).CoverProtection == cover
                && restored.ActionFor(1).EffectiveCoverProtection == 7,
            "Cover survives encounter JSON persistence"
        );
        await store.DeleteAsync(custom.Id);
        await db.Database.MigrateAsync();
        Check(
            !(await store.ListAsync()).Any(c => c.Id == custom.Id)
                && restored.ActionFor(1).CoverProtection == cover,
            "Delete preserves saved encounter and migration does not reseed deleted covers"
        );
        restored.ResetActions(1, roster);
        Check(restored.ActionFor(1).CoverProtection == cover, "Reset preserves resolved cover");
        restored.SetCoverStatus(1, true, false, roster);
        Check(
            restored.ActionFor(1).EffectiveCoverProtection == 0
                && restored.CanChooseAction(1, "Ranged Attack"),
            "Behind Cover permits engagement without protection"
        );
        encounter.ChooseAction(2, "Ranged Attack", roster);
        encounter.SetTarget(2, 1, roster);
        var loadout = new CombatLoadout([
            new CharacterEquipmentDetails
            {
                Equipped = true,
                Equipment = new EquipmentItem
                {
                    Kind = EquipmentKind.Weapon,
                    Name = "Rifle",
                    Range = 100,
                },
            },
        ]);
        var modifiers = AttackModifierCalculator.Calculate(encounter, foe, roster, loadout)!;
        Check(
            modifiers.Rows.Count(r => r.Name is "Target behind cover" or "Target Hiding") == 1
                && modifiers.Rows.Single(r => r.Name == "Target Hiding").Dm == -2,
            "Hiding and Behind Cover use a single DM -2"
        );
        restored.SetCoverStatus(1, true, true, roster);
        restored.SetCoverProtection(1, null, roster);
        restored.ResolveActions(1, roster);
        Check(
            restored.ActionFor(1).CoverProtection is null
                && restored.ActionFor(1).EffectiveCoverProtection == 0,
            "None clears selected cover"
        );
        Console.WriteLine("Cover protection checks passed.");
    }
}
