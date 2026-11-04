namespace MTU.Core;

public sealed partial class Encounter
{
    private void RequireUnresolved(int id)
    {
        if (ActionFor(id).ActionsResolved)
            throw new ArgumentException("Reset the resolved actions before choosing new actions.");
    }

    public void ResolveActions(int id, IReadOnlyList<SceneCombatant> entries)
    {
        var actor = RequireActor(id, entries);
        var state = ActionFor(id);
        if (state.ActionsResolved)
            return;
        if (state.MinorCount == 0 && state.MajorAction is null)
            throw new ArgumentException("Choose actions to resolve.");
        SceneCombatant? target = null;
        if (state.NeedsReaction)
        {
            target = entries.FirstOrDefault(e => e.Id == state.TargetId && e.IsNpc != actor.IsNpc);
            if (target is null || IsDead(target))
                throw new ArgumentException("Choose a living target before resolving the attack.");
        }
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
        };
        actions[id] = state with { MovementDiameter = MovementDiameterFor(state) };
    }
}
