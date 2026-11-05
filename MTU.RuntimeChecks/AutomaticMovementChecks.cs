using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class AutomaticMovementChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        var actor = new SceneCombatant(1, 1, "Hero", false, false, true, 0, 0, "777777");
        var foe = new SceneCombatant(2, 2, "Foe", true, false, true, 1, 0, "777777");
        var roster = new[] { actor, foe };
        Encounter Fresh()
        {
            var e = new Encounter();
            e.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
            return e;
        }
        bool Move(Encounter e, int column, int row)
        {
            var c = e.AutomaticMoveFor(roster)!;
            return e.SetAutomaticMovement(1, column, row, c.Round, c.Column, c.Row, roster);
        }
        var e = Fresh();
        var before = e.ActionFor(1);
        Check(
            Move(e, 2, 4)
                && e.ActionFor(1).MoveSelections == 1
                && e.ActionFor(1).MovementDiameter == 13.5,
            "Direct distance 6.7m falls within one Move plus tolerance"
        );
        Check(
            Move(e, 4, 7)
                && e.ActionFor(1).MoveSelections == 2
                && e.ActionFor(1).MovementDiameter == 25.5,
            "Direct distance 12.1m selects Move 2"
        );
        Check(
            Move(e, 5, 7)
                && e.ActionFor(1).MoveSelections == 3
                && e.ActionFor(1).MovementDiameter == 37.5,
            "Distance beyond 12.8m selects Move 3"
        );
        Check(
            Move(e, 1, 0) && e.ActionFor(1).MoveSelections == 1,
            "Dragging back reduces Move count"
        );
        e.CancelAutomaticMovement(1, before);
        Check(
            !e.ActionFor(1).AutomaticMove
                && e.ActionFor(1).MinorCount == 0
                && e.ActionFor(1).MovementDiameter is null,
            "Cancelling first drag restores no actions and no circle"
        );
        Move(e, 4, 7);
        var previous = e.ActionFor(1);
        Move(e, 7, 7);
        e.CancelAutomaticMovement(1, previous);
        Check(
            e.ActionFor(1).MoveSelections == 2,
            "Cancelling a later drag restores its prior Move count"
        );
        var repositioned = new[] { actor with { Column = 4, Row = 7 }, foe };
        var context = e.AutomaticMoveFor(repositioned)!;
        e.SetAutomaticMovement(1, 1, 0, context.Round, context.Column, context.Row, repositioned);
        Check(
            e.ActionFor(1).MoveSelections == 1
                && e.ActionFor(1).RangeColumn == 0
                && e.ActionFor(1).RangeRow == 0,
            "Repeated drop adjustments retain turn origin, not last drop"
        );
        var restored = new Encounter();
        restored.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(JsonSerializer.Serialize(e.Snapshot()))!,
            repositioned
        );
        Check(
            restored.ActionFor(1).AutomaticMove
                && restored.AutomaticMoveFor(repositioned)!.Column == 0,
            "Automatic movement and origin survive reload"
        );
        e.ChooseAction(1, "Aim", roster);
        Check(
            e.AutomaticMoveFor(roster) is null,
            "Manual action selection ends automatic movement control"
        );
        var manual = Fresh();
        manual.ChooseAction(1, "Leadership", roster, 2);
        Check(
            manual.AutomaticMoveFor(roster) is null,
            "Other selected actions prevent automatic Move"
        );
        var other = Fresh();
        var c = other.AutomaticMoveFor(roster)!;
        Check(
            !other.SetAutomaticMovement(2, 4, 4, c.Round, c.Column, c.Row, roster),
            "Dragging another combatant does not choose actions"
        );
        Check(
            !other.SetAutomaticMovement(1, 4, 4, c.Round + 1, c.Column, c.Row, roster),
            "Stale drag updates cannot cross rounds"
        );
        other.Next(roster);
        Check(!other.ActionFor(1).AutomaticMove, "Automatic movement ends with the turn");
        var fastRoster = new[] { actor with { Speed = 9 }, foe };
        var fast = Fresh();
        var fastContext = fast.AutomaticMoveFor(fastRoster)!;
        fast.SetAutomaticMovement(
            1,
            5,
            0,
            fastContext.Round,
            fastContext.Column,
            fastContext.Row,
            fastRoster
        );
        Check(
            fast.ActionFor(1).MoveSelections == 1 && fast.ActionFor(1).MovementDiameter == 19.5,
            "Automatic actions and circle both use the current character Speed"
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
        db.Campaigns.Add(new CampaignEntity { Id = 1, Name = "Auto Move" });
        db.Characters.Add(
            new CharacterEntity
            {
                Id = 1,
                CampaignId = 1,
                Name = "Hero",
                Upp = "777777",
            }
        );
        await db.SaveChangesAsync();
        var scenes = scope.ServiceProvider.GetRequiredService<SceneService>();
        var scene = await scenes.SaveAsync(
            new SceneInput { CampaignId = 1, Name = "Map" },
            SceneImageChecks.Png(16, 16)
        );
        await scenes.MoveTravellersHereAsync(1, scene.Id);
        var token = (await scenes.CombatantsAsync(1, scene.Id)).Single();
        await scenes.PositionCombatantAsync(1, scene.Id, token.Id, 0, 0);
        var snapshot = Fresh().Snapshot();
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER RejectMoveState BEFORE UPDATE OF EncounterState ON Scenes BEGIN SELECT RAISE(ABORT, 'Simulated save failure'); END;"
        );
        try
        {
            await scenes.PositionCombatantAsync(1, scene.Id, token.Id, 3, 4, snapshot);
            throw new Exception("Save unexpectedly succeeded");
        }
        catch (DbUpdateException) { }
        Check(
            (await scenes.CombatantsAsync(1, scene.Id)).Single() is { Column: 0, Row: 0 }
                && (await scenes.LoadEncounterAsync(1, scene.Id))!.Active == false,
            "Move and encounter-state updates roll back together on save failure"
        );
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectMoveState");
        await scenes.PositionCombatantAsync(1, scene.Id, token.Id, 3, 4, snapshot);
        Check(
            (await scenes.CombatantsAsync(1, scene.Id)).Single() is { Column: 3, Row: 4 }
                && (await scenes.LoadEncounterAsync(1, scene.Id))!.Active,
            "Move and encounter-state save together"
        );
        var reset = Fresh();
        Move(reset, 3, 4);
        var origin = reset.ActionFor(1);
        await scenes.PositionCombatantAsync(1, scene.Id, token.Id, 3, 4, reset.Snapshot());
        reset.ResetActions(1, roster);
        await scenes.PositionCombatantAsync(
            1,
            scene.Id,
            token.Id,
            origin.TurnOriginColumn,
            origin.TurnOriginRow,
            reset.Snapshot()
        );
        var resetState = (await scenes.LoadEncounterAsync(1, scene.Id))!.Actions![1];
        Check(
            (await scenes.CombatantsAsync(1, scene.Id)).Single() is { Column: 0, Row: 0 }
                && resetState.MinorCount == 0
                && resetState.MovementDiameter is null
                && resetState.TurnOriginColumn == 0
                && resetState.TurnOriginRow == 0,
            "Reset returns token to saved turn origin and persists cleared actions atomically"
        );
        var committed = Fresh();
        Move(committed, 9, 0);
        var movedRoster = roster
            .Select(e => e.Id == 1 ? e with { Column = 9, Row = 0 } : e)
            .ToArray();
        committed.ResolveActions(1, movedRoster);
        Check(
            committed.ActionFor(1).ActionsResolved
                && committed.ActionFor(1).MovementDiameter is null
                && committed.MovementRanges(movedRoster).Count == 0
                && committed.AutomaticMoveFor(movedRoster) is null,
            "Resolving Move 3 clears movement circles and automatic movement previews"
        );
        Check(
            committed.ActionFor(1).TurnOriginColumn == 9
                && committed.ActionFor(1).TurnOriginRow == 0,
            "Resolution commits the current token position as the turn origin"
        );
        var resumed = new Encounter();
        resumed.Restore(committed.Snapshot(), movedRoster);
        Check(
            resumed.MovementRanges(movedRoster).Count == 0,
            "Committed movement circles stay cleared after restore"
        );
        resumed.ResetActions(1, movedRoster);
        Check(
            resumed.ActionFor(1).TurnOriginColumn == 9 && resumed.ActionFor(1).TurnOriginRow == 0,
            "Reset after resolution retains the committed destination rather than the old origin"
        );
        Console.WriteLine(
            "Automatic Move distance/tolerance, reduction, origin persistence, action guards and atomic drag saves passed."
        );
    }
}
