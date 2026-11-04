namespace MTU.Core;

public sealed partial class Encounter
{
    private void RequireUnresolved(int id)
    {
        if (ActionFor(id).ActionsResolved)
            throw new ArgumentException("Reset the resolved actions before choosing new actions.");
    }

    public void ResolveActions(
        int id,
        IReadOnlyList<SceneCombatant> entries,
        bool smartTracker = false
    )
    {
        var actor = RequireActor(id, entries);
        var state = ActionFor(id);
        if (state.ActionsResolved)
            return;
        if (state.MinorCount == 0 && state.MajorAction is null && !state.HasPendingStatus)
            throw new ArgumentException("Choose actions to resolve.");
        if (state.SelectedHiding && state.MajorAction is "Melee Attack" or "Ranged Attack")
            throw new ArgumentException("Break from cover before making an attack.");
        SceneCombatant? target = null;
        if (state.NeedsReaction || state.IsSelected("Tag Target"))
        {
            target = entries.FirstOrDefault(e => e.Id == state.TargetId && e.IsNpc != actor.IsNpc);
            if (target is null || IsDead(target))
                throw new ArgumentException("Choose a living target before resolving the action.");
        }
        if (state.IsSelected("Tag Target") && (!smartTracker || !state.TagTargetHitConfirmed))
            throw new ArgumentException(
                "Tag Target requires an equipped Smart Tracker and a confirmed hit on the target."
            );
        if (target is not null && state.TargetReaction != "None")
        {
            var defence = ActionFor(target.Id);
            var stance =
                state.TargetReaction == "Dive for Prone" ? "Prone"
                : state.TargetReaction == "Dive for Cover"
                    ? state.TargetReactionStance ?? defence.Stance
                : defence.Stance;
            defence = defence with { ReactionCount = defence.ReactionCount + 1, Stance = stance };
            actions[target.Id] = defence with { MovementDiameter = MovementDiameterFor(defence) };
        }
        state = state with
        {
            Stance = state.SelectedStance,
            PendingStance = null,
            ActionsResolved = true,
            ResolvedAimBonus = state.AimBonus,
            BehindCover = state.SelectedBehindCover,
            Hiding = state.SelectedHiding,
            PendingBehindCover = null,
            PendingHiding = null,
            Elevated = state.SelectedElevated,
            PendingElevated = null,
            TargetTagged = state.TargetTagged || state.IsSelected("Tag Target"),
        };
        actions[id] = state with { MovementDiameter = MovementDiameterFor(state) };
    }
}
