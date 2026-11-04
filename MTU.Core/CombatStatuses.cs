namespace MTU.Core;

public sealed partial class Encounter
{
    public void SetCoverStatus(
        int id,
        bool behindCover,
        bool hiding,
        IReadOnlyList<SceneCombatant> entries
    )
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        var state = ActionFor(id) with { PendingBehindCover = behindCover, PendingHiding = hiding };
        if (hiding && state.MajorAction is "Melee Attack" or "Ranged Attack")
            state = ReleaseTargetReaction(state with { MajorAction = null });
        if (hiding)
            state = state with
            {
                BeforeMajorActions = state
                    .ColumnActions(1)
                    .Where(a => a is not ("Aim" or "Tag Target"))
                    .ToArray(),
                AfterMajorActions = state
                    .ColumnActions(3)
                    .Where(a => a is not ("Aim" or "Tag Target"))
                    .ToArray(),
                MinorActions = (state.MinorActions ?? [])
                    .Where(a => a is not ("Aim" or "Tag Target"))
                    .ToArray(),
                AimSelections = 0,
            };
        actions[id] = state;
    }

    public void SetElevated(int id, bool elevated, IReadOnlyList<SceneCombatant> entries)
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        actions[id] = ActionFor(id) with { PendingElevated = elevated };
    }
}
