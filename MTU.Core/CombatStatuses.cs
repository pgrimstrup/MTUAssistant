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
        actions[id] = state;
    }

    public void SetElevated(int id, bool elevated, IReadOnlyList<SceneCombatant> entries)
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        actions[id] = ActionFor(id) with { PendingElevated = elevated };
    }

    public void ConfirmTagTargetHit(int id, bool confirmed, IReadOnlyList<SceneCombatant> entries)
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        if (!ActionFor(id).IsSelected("Tag Target"))
            throw new ArgumentException("Select Tag Target first.");
        actions[id] = ActionFor(id) with { TagTargetHitConfirmed = confirmed };
    }
}
