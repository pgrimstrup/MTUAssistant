using System.Text.RegularExpressions;

namespace MTU.Core;

public sealed record AutoAttackTarget(
    int? TargetId = null,
    string Reaction = "None",
    string? Stance = null,
    int? Column = null,
    int? Row = null,
    CombatCover? Cover = null
);

public sealed record TargetAttackResult(
    int TargetIndex,
    int TargetId,
    AttackOutcome Outcome,
    AttackModifierSummary Modifiers
);

public static class AutoFire
{
    public static int Score(string? traits)
    {
        var match = Regex.Match(
            traits ?? "",
            @"\bAuto\s*(?:\(\s*)?(\d+)\b",
            RegexOptions.IgnoreCase
        );
        return int.TryParse(match.Groups[1].Value, out var score) && score > 0 ? score : 0;
    }
}

public sealed partial class Encounter
{
    private static CombatantActionState ClearTargets(CombatantActionState state) =>
        state with
        {
            TargetId = null,
            TargetReaction = "None",
            TargetReactionStance = null,
            TargetReactionCover = null,
            ReactionColumn = null,
            ReactionRow = null,
            ExtraTargets = state.ExtraTargets?.Select(_ => new AutoAttackTarget()).ToArray(),
            TargetTagged = false,
            TargetHasBeenHit = false,
            AttackOutcome = null,
            AutoAttackResults = null,
            ResolvedAttackModifiers = null,
            PreviewTargetIndex = 0,
        };

    public void SetAttackReactionCover(
        int id,
        int index,
        CombatCover? cover,
        IReadOnlyList<SceneCombatant> entries
    )
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        var slot = ActionFor(id).AttackTargets.ElementAtOrDefault(index);
        if (slot?.TargetId is null || slot.Reaction != "Dive to Hide")
            throw new ArgumentException("Choose Dive to Hide for a target first.");
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
        if (index == 0)
            actions[id] = ActionFor(id) with { TargetReactionCover = cover };
        else
            UpdateExtraTarget(id, index, slot with { Cover = cover });
    }

    public static bool WithinAutoTargetRange(SceneCombatant? first, SceneCombatant candidate) =>
        first is { OnMap: true }
        && candidate.OnMap
        && Math.Pow(first.Column!.Value - candidate.Column!.Value, 2)
            + Math.Pow(first.Row!.Value - candidate.Row!.Value, 2)
            <= 16;

    public void PruneAutoTargets(int id, IReadOnlyList<SceneCombatant> entries)
    {
        var state = ActionFor(id);
        if (!state.FullAuto || state.ActionsResolved)
            return;
        var first = entries.FirstOrDefault(e => e.Id == state.TargetId);
        actions[id] = state with
        {
            ExtraTargets = state
                .ExtraTargets?.Select(slot =>
                    entries.FirstOrDefault(e => e.Id == slot.TargetId) is { } target
                    && target.Id != first?.Id
                    && WithinAutoTargetRange(first, target)
                        ? slot
                        : new AutoAttackTarget()
                )
                .ToArray(),
        };
    }

    public CombatantActionState AttackStateFor(int id, int index = 0)
    {
        var state = ActionFor(id);
        if (index == 0)
            return state;
        var target =
            state.AttackTargets.ElementAtOrDefault(index)
            ?? throw new ArgumentException("Choose an available target slot.");
        return state with
        {
            TargetId = target.TargetId,
            TargetReaction = target.Reaction,
            TargetReactionStance = target.Stance,
            ReactionColumn = target.Column,
            ReactionRow = target.Row,
            TargetReactionCover = target.Cover,
            TargetTagged = false,
            TargetHasBeenHit = false,
            ResolvedAttackModifiers = null,
        };
    }

    public void SetAutoAttack(int id, string mode, int score, IReadOnlyList<SceneCombatant> entries)
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        if (
            mode is not ("Single" or "Burst" or "Full Auto")
            || ActionFor(id).MajorAction != "Ranged Attack"
            || (mode != "Single" && score <= 0)
        )
            throw new ArgumentException("Choose an available Auto Attack mode.");
        var state = ActionFor(id);
        state = state with
        {
            AutoAttack = mode,
            AutoScore = score,
            ExtraTargets =
                mode == "Full Auto"
                    ? Enumerable
                        .Range(0, Math.Max(0, score - 1))
                        .Select(i =>
                            state.ExtraTargets?.ElementAtOrDefault(i) ?? new AutoAttackTarget()
                        )
                        .ToArray()
                    : [],
            AutoAttackResults = null,
            AttackOutcome = null,
            PreviewTargetIndex = 0,
        };
        if (state.AutomaticFire)
            state = state with
            {
                BeforeMajorActions = state.ColumnActions(1).Where(a => a != "Aim").ToArray(),
                AfterMajorActions = state.ColumnActions(3).Where(a => a != "Aim").ToArray(),
                MinorActions = (state.MinorActions ?? []).Where(a => a != "Aim").ToArray(),
                AimSelections = 0,
                CarriedAimBonus = 0,
                ResolvedAimBonus = null,
            };
        actions[id] = state;
    }

    public void RefreshAutoWeapon(int id, int score, IReadOnlyList<SceneCombatant> entries)
    {
        var state = ActionFor(id);
        if (state.ActionsResolved || state.AutoAttack == "Single" || state.MajorAction is null)
            return;
        if (state.MajorAction != "Ranged Attack" || score <= 0)
        {
            actions[id] = state with { AutoAttack = "Single", AutoScore = 0, ExtraTargets = [] };
            return;
        }
        if (state.AutoScore != score)
            SetAutoAttack(id, state.AutoAttack, score, entries);
    }

    public void SetAttackTarget(
        int id,
        int index,
        int? targetId,
        IReadOnlyList<SceneCombatant> entries
    )
    {
        if (index == 0)
        {
            SetTarget(id, targetId, entries);
            PruneAutoTargets(id, entries);
            return;
        }
        var actor = RequireActor(id, entries);
        RequireUnresolved(id);
        if (targetId is int target && !entries.Any(e => e.Id == target && e.IsNpc != actor.IsNpc))
            throw new ArgumentException("Choose an opposing combatant.");
        if (
            targetId is int chosen
            && !WithinAutoTargetRange(
                entries.FirstOrDefault(e => e.Id == ActionFor(id).TargetId),
                entries.First(e => e.Id == chosen)
            )
        )
            throw new ArgumentException("Secondary targets must be within 6m of the first target.");
        UpdateExtraTarget(id, index, new(targetId));
    }

    private void UpdateExtraTarget(int id, int index, AutoAttackTarget target)
    {
        var state = ActionFor(id);
        var extras = (state.ExtraTargets ?? []).ToArray();
        if (!state.FullAuto || index < 1 || index > extras.Length)
            throw new ArgumentException("Choose an available target slot.");
        extras[index - 1] = target;
        actions[id] = state with { ExtraTargets = extras };
    }

    public void SetAttackReaction(
        int id,
        int index,
        string reaction,
        IReadOnlyList<SceneCombatant> entries
    )
    {
        if (index == 0)
        {
            SetTargetReaction(id, reaction, entries);
            return;
        }
        RequireActor(id, entries);
        RequireUnresolved(id);
        if (!TargetReactions.Contains(reaction))
            throw new ArgumentException("Choose an available reaction.");
        var slot =
            ActionFor(id).AttackTargets.ElementAtOrDefault(index)
            ?? throw new ArgumentException("Choose a target slot.");
        var target =
            entries.FirstOrDefault(e => e.Id == slot.TargetId)
            ?? throw new ArgumentException("Choose a target first.");
        if (IsDead(target))
            throw new ArgumentException("Choose a living target first.");
        if (slot.Reaction == reaction)
            return;
        UpdateExtraTarget(
            id,
            index,
            slot with
            {
                Reaction = reaction,
                Cover = reaction == "Dive to Hide" ? slot.Cover : null,
                Stance =
                    reaction == "Dive for Prone" ? "Prone"
                    : reaction is "Dive for Cover" or "Dive to Hide"
                        ? slot.Stance ?? ActionFor(target.Id).Stance
                    : null,
                Column = reaction == "None" ? null : slot.Column ?? target.Column,
                Row = reaction == "None" ? null : slot.Row ?? target.Row,
            }
        );
    }

    public void SetAttackReactionStance(
        int id,
        int index,
        string stance,
        IReadOnlyList<SceneCombatant> entries
    )
    {
        if (index == 0)
        {
            SetTargetReactionStance(id, stance, entries);
            return;
        }
        RequireActor(id, entries);
        RequireUnresolved(id);
        var slot = ActionFor(id).AttackTargets.ElementAtOrDefault(index);
        if (
            slot is null
            || slot.Reaction is not ("Dive for Cover" or "Dive to Hide")
            || !CombatActionChoices.Stances.Contains(stance)
        )
            throw new ArgumentException("Choose a Dive for Cover or Dive to Hide stance.");
        UpdateExtraTarget(id, index, slot with { Stance = stance });
    }

    public void ApplyAttacks(
        IReadOnlyList<AttackResolution> attacks,
        IReadOnlyList<SceneCombatant> entries,
        bool smartTracker = false
    )
    {
        if (attacks.Count == 0)
            throw new ArgumentException("Choose at least one target.");
        var actorId = attacks[0].ActorId;
        RequireActor(actorId, entries);
        RequireUnresolved(actorId);
        var state = ActionFor(actorId);
        var selected = state
            .AttackTargets.Select((slot, index) => (slot, index))
            .Where(t => t.slot.TargetId.HasValue)
            .ToArray();
        if (
            attacks.Count != selected.Length
            || attacks.Select(a => a.TargetId).Distinct().Count() != attacks.Count
        )
            throw new ArgumentException(
                "Confirm each selected target once, using different targets."
            );
        foreach (var attack in attacks)
        {
            if (
                attack.ActorId != actorId
                || selected.All(t =>
                    t.index != attack.TargetIndex || t.slot.TargetId != attack.TargetId
                )
                || !attack.Ready
            )
                throw new ArgumentException("Complete the rolls for each selected target.");
            if (state.MajorAction is not ("Ranged Attack" or "Melee Attack"))
                throw new ArgumentException("The attack selection has changed.");
            if (!entries.Any(e => e.Id == attack.TargetId))
                throw new ArgumentException("A target is no longer in this scene.");
        }
        var primary = attacks.Single(a => a.TargetIndex == 0);
        ResolveActions(actorId, entries, smartTracker, primary.Modifiers);
        foreach (var attack in attacks.Where(a => a.TargetIndex != 0))
            CommitTargetReaction(
                AttackStateFor(actorId, attack.TargetIndex),
                entries.Single(e => e.Id == attack.TargetId)
            );
        var results = attacks
            .Select(a => new TargetAttackResult(
                a.TargetIndex,
                a.TargetId,
                a.Outcome! with
                {
                    AppliedInjury = a.Outcome!.Hit ? a.Injury : 0,
                },
                a.Modifiers
            ))
            .ToArray();
        actions[actorId] = ActionFor(actorId) with
        {
            AttackOutcome = results.Single(r => r.TargetIndex == 0).Outcome,
            PreviewTargetIndex = 0,
            AutoAttackResults = state.FullAuto ? results : null,
            TargetHasBeenHit = state.TargetHasBeenHit || primary.Outcome!.Hit,
        };
        foreach (var result in results.Where(r => r.Outcome.Hit))
        {
            var target = entries.Single(e => e.Id == result.TargetId);
            SetHp(
                target.Id,
                Math.Max(0, Hp(target) - result.Outcome.AppliedInjury!.Value),
                entries
            );
        }
    }
}
