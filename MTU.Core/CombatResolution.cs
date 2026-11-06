namespace MTU.Core;

public sealed partial class Encounter
{
    private void CommitTargetReaction(CombatantActionState state, SceneCombatant target)
    {
        if (state.TargetReaction == "None")
            return;
        var defence = ActionFor(target.Id);
        var stance =
            state.TargetReaction == "Dive for Prone" ? "Prone"
            : state.TargetReaction == "Dive for Cover"
                ? state.TargetReactionStance ?? defence.Stance
            : defence.Stance;
        defence = defence with
        {
            ReactionCount = defence.ReactionCount + 1,
            Stance = stance,
            BehindCover = defence.BehindCover || state.TargetReaction == "Dive for Cover",
            Hiding = defence.Hiding || state.TargetReaction == "Dive for Cover",
            CoverProtection =
                state.TargetReaction == "Dive for Cover"
                    ? state.TargetReactionCover
                    : defence.CoverProtection,
        };
        if (state.TargetReaction == "Dive for Cover")
            defence = ClearTargets(defence);
        actions[target.Id] = defence with { MovementDiameter = MovementDiameterFor(defence) };
    }

    private void RequireUnresolved(int id)
    {
        if (ActionFor(id).ActionsResolved)
            throw new ArgumentException("Reset the resolved actions before choosing new actions.");
    }

    public void ResolveActions(
        int id,
        IReadOnlyList<SceneCombatant> entries,
        bool smartTracker = false,
        AttackModifierSummary? attackModifiers = null
    )
    {
        var actor = RequireActor(id, entries);
        var state = ActionFor(id);
        if (state.ActionsResolved)
            return;
        if (
            state.MajorAction == "Ranged Attack"
            && attackModifiers?.UnavailableReason is { } reason
        )
            throw new ArgumentException(reason);
        if (state.MinorCount == 0 && state.MajorAction is null && !state.HasPendingStatus)
            throw new ArgumentException("Choose actions to resolve.");
        if (
            state.SelectedHiding
            && (
                state.IsSelected("Aim")
                || state.IsSelected("Tag Target")
                || state.MajorAction is "Melee Attack" or "Ranged Attack"
            )
        )
            throw new ArgumentException(
                "Stop Hiding before attacking, aiming or tagging a target."
            );
        SceneCombatant? target = null;
        if (state.NeedsReaction || state.IsSelected("Tag Target"))
        {
            target = entries.FirstOrDefault(e => e.Id == state.TargetId && e.IsNpc != actor.IsNpc);
            if (target is null)
                throw new ArgumentException("Choose a target before resolving the action.");
        }
        if (state.IsSelected("Tag Target") && (!smartTracker || !state.TargetHasBeenHit))
            throw new ArgumentException(
                "Tag Target requires an equipped Smart Tracker and a target that has been hit."
            );
        if (target is not null)
            CommitTargetReaction(state, target);
        state = state with
        {
            Stance = state.SelectedStance,
            PendingStance = null,
            ActionsResolved = true,
            AutomaticMove = false,
            RangeColumn = null,
            RangeRow = null,
            TurnOriginColumn = actor.Column,
            TurnOriginRow = actor.Row,
            ResolvedAimBonus = state.AimBonus,
            BehindCover = state.SelectedBehindCover,
            Hiding = state.SelectedHiding,
            CoverProtection = state.SelectedCoverProtection,
            PendingCoverProtection = null,
            HasPendingCoverProtection = false,
            PendingBehindCover = null,
            PendingHiding = null,
            Elevated = state.SelectedElevated,
            PendingElevated = null,
            ResolvedAttackModifiers = state.MajorAction is "Ranged Attack" or "Melee Attack"
                ? attackModifiers
                : null,
            TargetTagged = state.TargetTagged || state.IsSelected("Tag Target"),
        };
        actions[id] = state with { MovementDiameter = MovementDiameterFor(state) };
    }
}
