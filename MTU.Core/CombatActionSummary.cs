namespace MTU.Core;

public sealed record CombatSummaryRow(string Label, string Value);

public sealed record CombatActionButton(string Text, bool Selected);

public sealed record CombatActionColumn(string Title, IReadOnlyList<CombatActionButton> Buttons);

public sealed record CombatAttackModifiers(
    IReadOnlyList<AttackModifier> Rows,
    int Total,
    string? UnavailableReason
);

public sealed record CombatActionSummary(
    int Id,
    int CharacterId,
    string Name,
    string Secondary,
    bool IsNpc,
    bool HasTokenImage,
    IReadOnlyList<CombatActionColumn> Columns,
    IReadOnlyList<CombatSummaryRow> Rows,
    CombatAttackModifiers? AttackModifiers = null
)
{
    public static string RangeDescription(string? band) =>
        band switch
        {
            "Short Range" => "Short Range (DM +1)",
            "Medium Range" => "Medium Range (DM 0)",
            "Long Range" => "Long Range (DM -2)",
            "Extreme Range" => "Extreme Range (DM -4)",
            "Out of Range" => "Out of Range — ranged attack cannot be made",
            _ => "",
        };

    public static CombatActionSummary? Create(
        Encounter encounter,
        SceneCombatant? actor,
        IReadOnlyList<SceneCombatant> entries,
        CombatLoadout? loadout,
        bool player = false
    )
    {
        if (actor is null || (player && (!encounter.Active || !actor.IsVisible)))
            return null;
        var state = encounter.ActionFor(actor.Id);
        var target = entries.FirstOrDefault(e =>
            e.Id == state.TargetId && (!player || e.IsVisible)
        );
        var band = loadout?.TargetBand(state, actor, target);
        var weapons = loadout?.WeaponsFor(state);
        var rows = new List<CombatSummaryRow> { new("Combatant", actor.Name) };
        if (!player || !actor.IsNpc)
        {
            rows.Add(
                new("Armour", string.IsNullOrEmpty(loadout?.Armour) ? "None" : loadout.Armour)
            );
            rows.Add(new("Weapon", string.IsNullOrEmpty(weapons) ? "None" : weapons));
        }
        rows.Add(
            new("Stance", state.SelectedStance == "Prone" ? "Prone (-1)" : state.SelectedStance)
        );
        rows.Add(
            new(
                "Reactions Made",
                state.ReactionCount == 0
                    ? "None"
                    : $"{state.ReactionCount} Reaction{(state.ReactionCount == 1 ? "" : "s")} (DM -{state.ReactionCount})"
            )
        );
        rows.Add(
            new(
                "Target",
                (target?.Name ?? "None") + (band is null ? "" : " — " + RangeDescription(band))
            )
        );
        if (state.SelectedHiding)
        {
            rows.Add(new("Cover", "Hiding"));
            rows.Add(
                new(
                    "Cover Protection",
                    state.SelectedCoverProtection is { } cover
                        ? $"{cover.Name} (+{state.EffectiveCoverProtection} protection; {cover.Hits} Hits)"
                        : "None selected"
                )
            );
        }
        else if (state.SelectedBehindCover)
            rows.Add(new("Cover", "Behind Cover"));
        if (state.Unconscious)
            rows.Add(new("Status", "Unconscious"));
        rows.Add(new("Sequence", state.SequenceSummary));
        rows.Add(
            new(
                state.MajorAction is "Ranged Attack" or "Melee Attack" ? "Attack Result" : "Result",
                state.AttackOutcome is { } outcome ? outcome.AttackSummary
                    : state.ActionsResolved ? "Actions resolved."
                    : band == "Out of Range"
                        ? "Ranged attack cannot be made: target is out of range."
                    : state.MovementDiameter.HasValue
                        ? actor.OnMap
                                ? "Movement range displayed until this turn ends. Action resolution pending."
                            : "Place the token to display its movement range."
                    : state.MajorAction is not null ? "Action selected; resolution pending."
                    : state.MinorCount > 0 ? "Minor actions selected."
                    : "Aim bonus carried from the previous turn."
            )
        );
        if (state.AttackOutcome is { Hit: true } hit)
        {
            var visibleDamage = target?.IsNpc == false ? hit : hit with { AppliedInjury = null };
            rows.Add(new("Damage Result", visibleDamage.DamageSummary!));
        }
        var columns = new[]
        {
            (1, "Minor Before", CombatActionChoices.Minor),
            (2, "Major Action", CombatActionChoices.Major),
            (3, "Minor After", CombatActionChoices.Minor),
        }
            .Select(g => new CombatActionColumn(
                g.Item2,
                g.Item3.Where(a => a != "Tag Target" || loadout?.HasSmartTracker == true)
                    .Select(a => new CombatActionButton(
                        state.ButtonText(a, g.Item1),
                        state.IsSelected(a, g.Item1)
                    ))
                    .ToArray()
            ))
            .ToArray();
        var modifiers =
            player && (actor.IsNpc || (state.TargetId.HasValue && target is null))
                ? null
                : AttackModifierCalculator.Calculate(encounter, actor, entries, loadout);
        return new(
            actor.Id,
            actor.CharacterId,
            actor.Name,
            actor.IsNpc ? actor.Occupation : actor.PlayerName,
            actor.IsNpc,
            actor.HasTokenImage,
            columns,
            rows,
            modifiers is null
                ? null
                : new(modifiers.Rows, modifiers.Total, modifiers.UnavailableReason)
        );
    }
}
