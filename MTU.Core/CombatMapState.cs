namespace MTU.Core;

public sealed partial class Encounter
{
    public static EncounterSnapshot ClearMapState(EncounterSnapshot snapshot, int id)
    {
        var cleared = new Dictionary<int, CombatantActionState>(snapshot.Actions ?? []);
        if (cleared.TryGetValue(id, out var state))
            cleared[id] = new CombatantActionState(
                Action: state.Action,
                MinorActions: state.MinorActions,
                MajorAction: state.MajorAction,
                AimSelections: state.AimSelections,
                MoveSelections: state.MoveSelections,
                BeforeMajorActions: state.BeforeMajorActions,
                AfterMajorActions: state.AfterMajorActions,
                ActionsResolved: state.ActionsResolved,
                RangeColour: state.RangeColour,
                Speed: state.Speed,
                AutoAttack: state.AutoAttack,
                AutoScore: state.AutoScore,
                ExtraTargets: state.ExtraTargets?.Select(_ => new AutoAttackTarget()).ToArray(),
                NaturalAttackId: state.NaturalAttackId
            );
        foreach (var pair in cleared.ToArray())
            if (pair.Key != id && pair.Value.TargetIds.Contains(id))
                cleared[pair.Key] = ClearTargets(pair.Value);
        return snapshot with { Actions = cleared };
    }
}
