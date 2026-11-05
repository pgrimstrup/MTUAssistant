namespace MTU.Core;

public sealed record AutomaticMoveContext(
    int Id,
    int Round,
    int Column,
    int Row,
    double Speed,
    int Moves,
    string Colour,
    bool IsVisible
);

public sealed partial class Encounter
{
    public AutomaticMoveContext? AutomaticMoveFor(IReadOnlyList<SceneCombatant> entries)
    {
        if (
            !Active
            || entries.FirstOrDefault(e => e.Id == SelectedId) is not { OnMap: true } actor
            || IsDead(actor)
        )
            return null;
        var state = ActionFor(actor.Id);
        if (
            state.ActionsResolved
            || (!state.AutomaticMove && (state.MinorCount != 0 || state.MajorAction is not null))
        )
            return null;
        var speed =
            actor.Speed
            * (
                state.SelectedStance == "Prone" ? .25
                : state.SelectedStance == "Crouching" ? .5
                : 1
            );
        if (
            speed <= 0
            || state.TurnOriginColumn is not int column
            || state.TurnOriginRow is not int row
        )
            return null;
        return new(
            actor.Id,
            Round,
            column,
            row,
            speed,
            state.MoveSelections,
            state.RangeColour,
            actor.IsVisible
        );
    }

    public bool SetAutomaticMovement(
        int id,
        int column,
        int row,
        int round,
        int originColumn,
        int originRow,
        IReadOnlyList<SceneCombatant> entries
    )
    {
        var context = AutomaticMoveFor(entries);
        if (
            context is null
            || context.Id != id
            || context.Round != round
            || context.Column != originColumn
            || context.Row != originRow
            || column < 0
            || row < 0
        )
            return false;
        var distance =
            Math.Sqrt(Math.Pow(column - originColumn, 2) + Math.Pow(row - originRow, 2)) * 1.5;
        var moves = (int)
            Math.Clamp(Math.Ceiling(Math.Max(0, distance - .8) / context.Speed - 1e-9), 1, 3);
        var state = ActionFor(id) with
        {
            Speed = entries.First(e => e.Id == id).Speed,
            AutomaticMove = true,
            Action = "Move",
            BeforeMajorActions = Enumerable.Repeat("Move", moves).ToArray(),
            AfterMajorActions = [],
            MinorActions = [],
            AimSelections = 0,
            MoveSelections = moves,
            RangeColumn = originColumn,
            RangeRow = originRow,
        };
        actions[id] = state with { MovementDiameter = MovementDiameterFor(state) };
        return true;
    }

    public void CancelAutomaticMovement(int id, CombatantActionState previous)
    {
        var current = ActionFor(id);
        if (
            current.AutomaticMove
            && current.TurnOriginColumn == previous.TurnOriginColumn
            && current.TurnOriginRow == previous.TurnOriginRow
        )
            actions[id] = previous;
    }
}
