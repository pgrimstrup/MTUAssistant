namespace MTU.Core;

public sealed record CombatSummaryRow(string Label, string Value);

public sealed record CombatActionButton(string Text, bool Selected);

public sealed record CombatActionColumn(string Title, IReadOnlyList<CombatActionButton> Buttons);

public sealed record CombatAttackModifiers(
    IReadOnlyList<AttackModifier> Rows,
    int Total,
    string? UnavailableReason
);

public sealed record CombatTargetAttackModifiers(
    int TargetId,
    string Name,
    CombatAttackModifiers Modifiers
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
    CombatAttackModifiers? AttackModifiers = null,
    IReadOnlyList<CombatTargetAttackModifiers>? TargetAttackModifiers = null
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
        var resultTarget = state.FullAuto
            ? entries.FirstOrDefault(e =>
                e.Id == state.AttackTargets.ElementAtOrDefault(state.PreviewTargetIndex)?.TargetId
                && (!player || e.IsVisible)
            )
            : target;
        var previewOutcome =
            player && state.FullAuto && resultTarget is null ? null : state.AttackOutcome;
        var resultSuffix = state.FullAuto ? $" {state.PreviewTargetIndex + 1}" : "";
        var band = loadout?.TargetBand(state, actor, target);
        var weapons = (loadout ?? new CombatLoadout([])).WeaponsFor(state);
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
        if (state.MajorAction == "Ranged Attack" && loadout?.AutoScore > 0)
            rows.Add(
                new(
                    "Auto Attack",
                    state.AutoAttack == "Burst" ? $"Burst ({loadout.AutoScore} rounds)"
                        : state.FullAuto ? $"Full Auto ({loadout.AutoScore * 3} rounds)"
                        : "Single"
                )
            );
        if (state.FullAuto)
            rows[rows.FindIndex(r => r.Label == "Target")] = new(
                "Targets",
                string.Join(
                    ", ",
                    state
                        .TargetIds.Select(id =>
                            entries
                                .FirstOrDefault(e => e.Id == id && (!player || e.IsVisible))
                                ?.Name
                        )
                        .Where(n => n != null)
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
        if (state.FullAuto && state.AutoAttackResults is { Count: > 0 } results)
        {
            foreach (var result in results)
            {
                var victim = entries.FirstOrDefault(e =>
                    e.Id == result.TargetId && (!player || e.IsVisible)
                );
                if (victim is null)
                    continue;
                rows.Add(
                    new($"Attack Result {result.TargetIndex + 1}", result.Outcome.AttackSummary)
                );
                if (result.Outcome.Hit)
                    rows.Add(
                        new(
                            $"Damage Result {result.TargetIndex + 1}",
                            (
                                victim.IsNpc
                                    ? result.Outcome with
                                    {
                                        AppliedInjury = null,
                                    }
                                    : result.Outcome
                            ).DamageSummary!
                        )
                    );
            }
        }
        else
        {
            rows.Add(
                new(
                    state.MajorAction is "Ranged Attack" or "Melee Attack"
                        ? "Attack Result" + resultSuffix
                        : "Result",
                    previewOutcome is { } outcome ? outcome.AttackSummary
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
            if (previewOutcome is { Hit: true } hit)
            {
                var visibleDamage =
                    resultTarget?.IsNpc == false ? hit : hit with { AppliedInjury = null };
                rows.Add(new("Damage Result" + resultSuffix, visibleDamage.DamageSummary!));
            }
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
        var targetModifiers =
            state.FullAuto && (!player || !actor.IsNpc)
                ? state
                    .AttackTargets.Select((slot, index) => (slot, index))
                    .Where(t =>
                        entries.Any(e => e.Id == t.slot.TargetId && (!player || e.IsVisible))
                    )
                    .Select(t =>
                    {
                        var value = AttackModifierCalculator.Calculate(
                            encounter,
                            actor,
                            entries,
                            loadout,
                            t.index
                        )!;
                        return new CombatTargetAttackModifiers(
                            t.slot.TargetId!.Value,
                            entries.First(e => e.Id == t.slot.TargetId).Name,
                            new(value.Rows, value.Total, value.UnavailableReason)
                        );
                    })
                    .ToArray()
                : null;
        return new(
            actor.Id,
            actor.CharacterId,
            actor.Name,
            actor.IsNpc ? actor.Occupation : actor.PlayerName,
            actor.IsNpc,
            actor.HasTokenImage,
            columns,
            rows,
            state.FullAuto || modifiers is null
                ? null
                : new(modifiers.Rows, modifiers.Total, modifiers.UnavailableReason),
            targetModifiers
        );
    }
}
