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
        if (hiding)
            behindCover = true;
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
        actions[id] = hiding ? ClearTargets(state) : state;
    }

    public void SetCoverProtection(
        int id,
        CombatCover? cover,
        IReadOnlyList<SceneCombatant> entries
    )
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        if (!ActionFor(id).SelectedHiding)
            throw new ArgumentException("Cover protection is available only while Hiding.");
        if (
            cover is not null
            && (
                cover.Id <= 0
                || string.IsNullOrWhiteSpace(cover.Name)
                || cover.Protection < 0
                || cover.Hits <= 0
            )
        )
            throw new ArgumentException("Choose valid cover protection.");
        actions[id] = ActionFor(id) with
        {
            PendingCoverProtection = cover,
            HasPendingCoverProtection = true,
        };
    }

    public void SetUnconscious(int id, bool unconscious, IReadOnlyList<SceneCombatant> entries)
    {
        RequireActor(id, entries);
        var state = ActionFor(id);
        state = state with
        {
            Unconscious = unconscious,
            Stance = unconscious ? "Prone" : state.Stance,
            PendingStance = unconscious ? null : state.PendingStance,
        };
        if (unconscious)
            state = ClearTargets(state);
        actions[id] = state with { MovementDiameter = MovementDiameterFor(state) };
    }

    public void SetFlying(int id, bool flying, IReadOnlyList<SceneCombatant> entries)
    {
        var actor = RequireActor(id, entries);
        RequireUnresolved(id);
        var state = ActionFor(id) with
        {
            PendingFlying = flying,
            Speed = AnimalCombat.Speed(actor, flying),
        };
        actions[id] = state with { MovementDiameter = MovementDiameterFor(state) };
    }

    public void SetNaturalAttack(int id, int attackId, IReadOnlyList<SceneCombatant> entries)
    {
        var actor = RequireActor(id, entries);
        RequireUnresolved(id);
        if (!actor.IsAnimal || attackId < 1 || attackId > (actor.NaturalAttacks?.Count ?? 0))
            throw new ArgumentException("Choose a natural attack.");
        actions[id] = ActionFor(id) with { NaturalAttackId = attackId };
    }

    public void SetElevated(int id, bool elevated, IReadOnlyList<SceneCombatant> entries)
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        actions[id] = ActionFor(id) with { PendingElevated = elevated };
    }
}
