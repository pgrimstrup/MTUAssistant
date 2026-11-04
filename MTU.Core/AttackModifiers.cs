using System.Text.Json.Serialization;

namespace MTU.Core;

public sealed record AttackModifier(string Name, int Dm);

public sealed record AttackModifierSummary(
    IReadOnlyList<AttackModifier> Rows,
    string? UnavailableReason = null
)
{
    [JsonIgnore]
    public int Total => Rows.Sum(row => row.Dm);

    [JsonIgnore]
    public bool CanResolve => UnavailableReason is null;
}

public static class AttackModifierCalculator
{
    public static AttackModifierSummary? Calculate(
        Encounter encounter,
        SceneCombatant actor,
        IReadOnlyList<SceneCombatant> entries,
        CombatLoadout? loadout
    )
    {
        var state = encounter.ActionFor(actor.Id);
        if (state.MajorAction != "Ranged Attack" && !state.IsSelected("Aim"))
            return null;
        if (state.ActionsResolved && state.ResolvedAttackModifiers is { } resolved)
            return resolved;
        var weapon = loadout?.RangedWeapon;
        if (weapon is null)
            return new([], "Equip a ranged weapon to calculate attack modifiers.");
        var rows = new List<AttackModifier>();
        var required = weapon.Equipment.RequiredSkill;
        rows.Add(
            new(
                required ?? "Weapon skill",
                string.IsNullOrWhiteSpace(required)
                    ? 0
                    : SkillRules.Dm(actor.Skills ?? [], required)
            )
        );
        rows.Add(new("DEX", Encounter.Dm(encounter.Health(actor).Dexterity)));
        var aim = state.MajorAction == "Ranged Attack" ? state.RangedAimBonus : state.AimBonus;
        if (aim > 0)
            rows.Add(new("Aim", aim));
        if (state.SelectedElevated)
            rows.Add(new("Elevated", 1));
        if (state.ReactionCount > 0)
            rows.Add(new("Reactions Made", -state.ReactionCount));
        var target = entries.FirstOrDefault(e => e.Id == state.TargetId);
        if (target is null)
            return new(rows, "Select a target to calculate attack modifiers.");
        if (!actor.OnMap || !target.OnMap)
            return new(rows, "Place both tokens to calculate range and sight modifiers.");
        var distance =
            Math.Sqrt(
                Math.Pow(target.Column!.Value - actor.Column!.Value, 2)
                    + Math.Pow(target.Row!.Value - actor.Row!.Value, 2)
            ) * 1.5;
        var rangedState =
            state.MajorAction == "Ranged Attack"
                ? state
                : new CombatantActionState(MajorAction: "Ranged Attack", CarriedAimBonus: aim);
        var band = loadout!.TargetBand(rangedState, actor, target);
        var rangeDm = band switch
        {
            "Short Range" => 1,
            "Long Range" => -2,
            "Extreme Range" => -4,
            _ => 0,
        };
        if (rangeDm != 0)
            rows.Add(new(band!, rangeDm));
        var targetState = encounter.ActionFor(target.Id);
        var stance =
            state.TargetReaction == "Dive for Prone" ? "Prone"
            : state.TargetReaction == "Dive for Cover"
                ? state.TargetReactionStance ?? targetState.Stance
            : targetState.Stance;
        var cover = targetState.BehindCover || state.TargetReaction == "Dive for Cover";
        if (stance == "Prone")
            rows.Add(new("Target prone", -1));
        if (cover)
            rows.Add(new("Target behind cover", -2));
        if (targetState.Hiding)
            rows.Add(new("Target Hiding", -2));
        if (state.TargetReaction == "Dodge")
        {
            var dex = Encounter.Dm(encounter.Health(target).Dexterity);
            var athletics = SkillRules.Rank(target.Skills ?? [], "Athletics (Dexterity)") ?? 0;
            rows.Add(new("Target Dodge", -Math.Max(1, Math.Max(dex, athletics))));
        }
        if (weapon.LaserSight && distance < 50)
            rows.Add(new("Laser Sights", 1));
        if (weapon.HolographicSight && distance >= 50)
            rows.Add(new("Holographic Sights", 1));
        if (weapon.SmartTracker && state.TargetTagged)
            rows.Add(new("Smart Tracker — tagged target", 2));
        if (weapon.Phud)
            rows.Add(new("P-HUD", 1));
        if (weapon.XRayOutliningAid && cover)
            rows.Add(new("X-Ray Outlining Aid", 2));
        return new(
            rows,
            band == "Out of Range" ? "Ranged attack cannot be made: target is out of range."
                : state.SelectedHiding ? "Stop Hiding before making an attack."
                : null
        );
    }
}
