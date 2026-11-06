namespace MTU.Core;

public sealed partial class Encounter
{
    public static readonly string[] TargetReactions =
    [
        "None",
        "Dodge",
        "Parry",
        "Dive for Cover",
        "Dive for Prone",
    ];

    private CombatantActionState ReleaseTargetReaction(CombatantActionState state)
    {
        return state with
        {
            TargetReaction = "None",
            TargetReactionCover = null,
            TargetReactionStance = null,
            ReactionColumn = null,
            ReactionRow = null,
        };
    }

    public void SetTargetReaction(int id, string reaction, IReadOnlyList<SceneCombatant> entries)
    {
        var actor = RequireActor(id, entries);
        RequireUnresolved(id);
        if (!TargetReactions.Contains(reaction))
            throw new ArgumentException("Choose an available target reaction.");
        var state = ActionFor(id);
        if (!state.NeedsReaction)
            throw new ArgumentException("Select a Melee Attack or Ranged Attack first.");
        var target = entries.FirstOrDefault(e => e.Id == state.TargetId && e.Id != actor.Id);
        if (target is null || IsDead(target))
            throw new ArgumentException("Choose a living target first.");
        if (state.TargetReaction == reaction)
            return;
        if (reaction == "None")
        {
            actions[id] = ReleaseTargetReaction(state);
            return;
        }
        actions[id] = state with
        {
            TargetReaction = reaction,
            TargetReactionCover = reaction == "Dive for Cover" ? state.TargetReactionCover : null,
            TargetReactionStance =
                reaction == "Dive for Prone" ? "Prone"
                : reaction == "Dive for Cover"
                    ? state.TargetReactionStance ?? ActionFor(target.Id).Stance
                : null,
            ReactionColumn = state.IsDive ? state.ReactionColumn ?? target.Column : target.Column,
            ReactionRow = state.IsDive ? state.ReactionRow ?? target.Row : target.Row,
        };
    }

    public void SetTargetReactionCover(
        int id,
        CombatCover? cover,
        IReadOnlyList<SceneCombatant> entries
    )
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        var state = ActionFor(id);
        if (!state.NeedsReaction || state.TargetReaction != "Dive for Cover")
            throw new ArgumentException("Choose Dive for Cover before selecting cover protection.");
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
        actions[id] = state with { TargetReactionCover = cover };
    }

    public void SetTargetReactionStance(
        int id,
        string stance,
        IReadOnlyList<SceneCombatant> entries
    )
    {
        RequireActor(id, entries);
        RequireUnresolved(id);
        var state = ActionFor(id);
        if (
            !state.NeedsReaction
            || state.TargetReaction != "Dive for Cover"
            || state.TargetId is not int target
        )
            throw new ArgumentException(
                "Select Dive for Cover to choose the target's reaction stance."
            );
        if (!CombatActionChoices.Stances.Contains(stance))
            throw new ArgumentException("Choose Standing, Crouching or Prone.");
        actions[id] = state with { TargetReactionStance = stance };
    }

    private IEnumerable<CombatMovementRange> TargetReactionRanges(
        IReadOnlyList<SceneCombatant> entries
    )
    {
        foreach (var actor in entries)
        {
            for (var index = 0; index < ActionFor(actor.Id).AttackTargets.Count; index++)
            {
                var state = AttackStateFor(actor.Id, index);
                if (!state.NeedsReaction || !state.IsDive)
                    continue;
                var target = entries.FirstOrDefault(e => e.Id == state.TargetId);
                if (target is not { OnMap: true } || IsDead(target))
                    continue;
                yield return new(
                    target.Id,
                    state.ReactionColumn ?? target.Column!.Value,
                    state.ReactionRow ?? target.Row!.Value,
                    4.5,
                    "#87cefa",
                    actor.IsVisible && target.IsVisible
                );
            }
        }
    }
}
