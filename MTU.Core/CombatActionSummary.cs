namespace MTU.Core;

public sealed record CombatSummaryRow(string Label, string Value);

public sealed record CombatActionButton(string Text, bool Selected);

public sealed record CombatActionColumn(string Title, IReadOnlyList<CombatActionButton> Buttons);

public sealed record CombatActionSummary(
    int Id,
    int CharacterId,
    string Name,
    string Secondary,
    bool IsNpc,
    bool HasTokenImage,
    IReadOnlyList<CombatActionColumn> Columns,
    IReadOnlyList<CombatSummaryRow> Rows
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
        var rows = new List<CombatSummaryRow> { new("Combatant", actor.Name) };
        if (!player || !actor.IsNpc)
        {
            rows.Add(
                new("Armour", string.IsNullOrEmpty(loadout?.Armour) ? "None" : loadout.Armour)
            );
            rows.Add(
                new("Weapon", string.IsNullOrEmpty(loadout?.Weapons) ? "None" : loadout.Weapons)
            );
        }
        rows.Add(
            new("Stance", state.SelectedStance == "Prone" ? "Prone (-1)" : state.SelectedStance)
        );
        var statuses = new List<string>();
        if (state.SelectedBehindCover)
            statuses.Add("Behind Cover");
        if (state.SelectedHiding)
            statuses.Add("Hiding");
        if (state.SelectedElevated)
            statuses.Add("Elevated");
        if (state.TargetTagged && (!player || target is not null))
            statuses.Add("Target Tagged");
        rows.Add(new("Status", statuses.Count == 0 ? "None" : string.Join(", ", statuses)));
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
        if (state.NeedsReaction && (!player || target is not null))
        {
            rows.Add(
                new(
                    "Target Reaction",
                    state.TargetReaction == "Dive for Cover" && target is not null
                        ? $"Dive for Cover ({state.TargetReactionStance ?? encounter.ActionFor(target.Id).Stance})"
                        : state.TargetReaction
                )
            );
        }
        rows.Add(new("Sequence", state.SequenceSummary));
        var aim = new List<string>();
        if (state.RangedAimBonus > 0)
            aim.Add($"Ranged Attack +{state.RangedAimBonus}");
        if (state.AimBonus > 0)
            aim.Add($"Carried Over +{state.AimBonus}");
        if (aim.Count > 0)
            rows.Add(new("Aim", string.Join(", ", aim)));
        rows.Add(
            new(
                "Result",
                state.ActionsResolved ? "Actions resolved."
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
        return new(
            actor.Id,
            actor.CharacterId,
            actor.Name,
            actor.IsNpc ? actor.Occupation : actor.PlayerName,
            actor.IsNpc,
            actor.HasTokenImage,
            columns,
            rows
        );
    }
}
