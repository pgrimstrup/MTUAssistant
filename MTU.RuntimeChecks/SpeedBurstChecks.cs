using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class SpeedBurstChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        static bool Throws(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }
        // END 5 is DM -1, so this Aslan has 3 - 1 = 2 speed bursts per encounter.
        var aslan = new SceneCombatant(
            1,
            1,
            "Aslan",
            false,
            false,
            true,
            0,
            0,
            "775777",
            Race: "Aslan"
        );
        var human = new SceneCombatant(
            2,
            2,
            "Human",
            true,
            false,
            true,
            4,
            0,
            "777777",
            Race: "Human"
        );
        var roster = new[] { aslan, human };
        Check(Encounter.SpeedBurstLimitFor(aslan) == 2, "Aslan END 5 has 3 + DM-1 bursts");
        Check(
            Encounter.SpeedBurstLimitFor(aslan with { Upp = "77A777", Race = " aslan " }) == 4,
            "Race match ignores case and spaces; END 10 gives 3 + DM+1"
        );
        Check(
            Encounter.SpeedBurstLimitFor(aslan with { Upp = "770777" }) == 0,
            "END 0 gives no bursts"
        );
        Check(Encounter.SpeedBurstLimitFor(human) == 0, "Non-Aslan combatants have no bursts");
        Check(
            Encounter.SpeedBurstLimitFor(aslan with { IsAnimal = true }) == 0,
            "Animals never get Aslan bursts"
        );

        Encounter Fresh()
        {
            var e = new Encounter();
            e.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
            return e;
        }
        var e = Fresh();
        Check(
            e.ActionFor(1).SpeedBurstLimit == 2 && e.ActionFor(1).SpeedBurstsUsed == 0,
            "Encounter start sets the burst limit with none used"
        );
        Check(
            Throws(() => e.SetSpeedBurst(2, true, roster)),
            "A combatant with no bursts cannot use Speed Burst"
        );
        e.ChooseAction(1, "Move", roster);
        Check(e.ActionFor(1).MovementDiameter == 13.5, "Normal Move without Speed Burst");
        e.SetSpeedBurst(1, true, roster);
        Check(
            e.ActionFor(1).MovementDiameter == 25.5 && e.ActionFor(1).SpeedBurstsAvailable == 1,
            "Speed Burst doubles Speed for the Move and counts it against the remaining bursts"
        );
        e.ChooseAction(1, "Move", roster);
        e.ChooseAction(1, "Move", roster);
        Check(
            e.ActionFor(1).MovementDiameter == 61.5 && e.ActionFor(1).SpeedBurstsAvailable == 0,
            "Two bursts and three Moves cover 12 + 12 + 6 m"
        );
        e.SetSpeedBurst(1, false, roster);
        Check(e.ActionFor(1).MovementDiameter == 37.5, "Unchecking restores normal Speed");
        e.SetSpeedBurst(1, true, roster);
        var restored = new Encounter();
        restored.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(JsonSerializer.Serialize(e.Snapshot()))!,
            roster
        );
        Check(
            restored.ActionFor(1).MovementDiameter == 61.5 && restored.ActionFor(1).SpeedBurst,
            "Speed Burst survives save/restore"
        );
        e.ResolveActions(1, roster);
        Check(e.ActionFor(1).SpeedBurstsUsed == 2, "Resolving Moves uses up the bursts");
        e.Next(roster);
        e.Next(roster);
        Check(
            e.SelectedId == 1
                && !e.ActionFor(1).SpeedBurst
                && e.ActionFor(1).SpeedBurstsUsed == 2
                && e.ActionFor(1).SpeedBurstsRemaining == 0,
            "Used bursts carry into later turns and the checkbox clears"
        );
        Check(Throws(() => e.SetSpeedBurst(1, true, roster)), "No Speed Burst once all are used");
        e.Exit(roster);
        e.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
        Check(e.ActionFor(1).SpeedBurstsRemaining == 2, "A new encounter resets used bursts");

        var unresolved = Fresh();
        unresolved.SetSpeedBurst(1, true, roster);
        unresolved.ChooseAction(1, "Move", roster);
        unresolved.Next(roster);
        Check(
            unresolved.ActionFor(1).SpeedBurstsUsed == 1,
            "Ending the turn without resolving still uses the burst"
        );
        var unused = Fresh();
        unused.SetSpeedBurst(1, true, roster);
        unused.Next(roster);
        Check(unused.ActionFor(1).SpeedBurstsUsed == 0, "Speed Burst without a Move uses nothing");

        var crouched = Fresh();
        crouched.ChooseAction(1, "Change Stance", roster);
        crouched.SetStance(1, "Crouching", roster);
        crouched.SetSpeedBurst(1, true, roster);
        crouched.ChooseAction(1, "Move", roster);
        Check(crouched.ActionFor(1).MovementDiameter == 13.5, "Stance scales the doubled Speed");

        var auto = Fresh();
        auto.SetSpeedBurst(1, true, roster);
        var context = auto.AutomaticMoveFor(roster)!;
        Check(context.BurstMoves == 2, "Automatic movement knows the available bursts");
        Check(
            auto.SetAutomaticMovement(1, 8, 0, context.Round, context.Column, context.Row, roster)
                && auto.ActionFor(1).MoveSelections == 1
                && auto.ActionFor(1).MovementDiameter == 25.5,
            "A 12 m drag needs one burst Move"
        );
        Check(
            Encounter.AutomaticMoveCount(12.8, 6, 0) == 2
                && Encounter.AutomaticMoveCount(12.8, 6, 2) == 1
                && Encounter.AutomaticMoveCount(24.8, 6, 2) == 2
                && Encounter.AutomaticMoveCount(24.8001, 6, 2) == 3
                && Encounter.AutomaticMoveCount(18.8, 6, 1) == 2
                && Encounter.AutomaticMoveCount(18.8001, 6, 1) == 3,
            "Automatic Move counts allow for burst Moves"
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
        db.Campaigns.Add(new CampaignEntity { Id = 1, Name = "Bursts" });
        await db.SaveChangesAsync();
        await new CharacterService(db).SaveAsync(
            new Character(0, 1, "", "Ftahalr", "Aslan", "775777", 0)
        );
        var scenes = scope.ServiceProvider.GetRequiredService<SceneService>();
        var scene = await scenes.SaveAsync(
            new SceneInput { CampaignId = 1, Name = "Burst scene" },
            SceneImageChecks.Png(16, 16)
        );
        await scenes.MoveTravellersHereAsync(1, scene.Id);
        var loaded = (await scenes.CombatantsAsync(1, scene.Id)).Single();
        Check(
            loaded.Race == "Aslan" && Encounter.SpeedBurstLimitFor(loaded) == 2,
            "Scene combatants load the character race"
        );
        Console.WriteLine(
            "Aslan speed bursts: limit, doubled Move circles, usage, turn/encounter reset, automatic movement and race loading passed."
        );
    }
}
