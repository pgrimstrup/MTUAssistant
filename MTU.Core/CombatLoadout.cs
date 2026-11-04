using System.Text.RegularExpressions;

namespace MTU.Core;

public sealed record CombatWeaponRange(double Range, bool Scope, int AimBonus)
{
    public string? Band(double distance)
    {
        if (Range <= 0)
            return null;
        if (distance > 100 && !(Scope && AimBonus >= 1))
            return "Extreme Range";
        return distance < Range * .25 ? "Short Range"
            : distance < Range ? "Medium Range"
            : distance < Range * 2 ? "Long Range"
            : distance < Range * 4 ? "Extreme Range"
            : "Out of Range";
    }
}

public sealed class CombatLoadout(IReadOnlyList<CharacterEquipmentDetails> equipment)
{
    private IEnumerable<CharacterEquipmentDetails> Equipped => equipment.Where(e => e.Equipped);
    public bool HasSmartTracker => Equipped.Any(e => e.SupportsAttachments && e.SmartTracker);
    public CharacterEquipmentDetails? RangedWeapon =>
        Equipped.FirstOrDefault(e => e.SupportsAttachments);
    public string Armour =>
        string.Join(
            ", ",
            Equipped
                .Where(e => e.Equipment.Kind == EquipmentKind.Armour)
                .Select(e => $"{e.Equipment.Name} (DR {e.Equipment.Protection:+0;-0;+0})")
        );
    public string Weapons =>
        string.Join(
            ", ",
            Equipped
                .Where(e => e.Equipment.Kind == EquipmentKind.Weapon)
                .Select(e =>
                    $"{e.Equipment.Name} ({(e.Equipment.Range > 0 ? $"{e.Equipment.Range:0.##}m" : "Melee")}, {e.Equipment.Damage})"
                )
        );

    public CombatWeaponRange? RangeFor(CombatantActionState state)
    {
        var weapon = RangedWeapon;
        return weapon is null
            ? null
            : new(
                weapon.Equipment.Range,
                weapon.Scope
                    || Regex.IsMatch(
                        weapon.Equipment.Traits,
                        @"\bScope\b",
                        RegexOptions.IgnoreCase
                    ),
                state.MajorAction == "Ranged Attack" ? state.RangedAimBonus : state.AimBonus
            );
    }

    public string? TargetBand(
        CombatantActionState state,
        SceneCombatant actor,
        SceneCombatant? target
    ) =>
        state.MajorAction == "Ranged Attack" && actor.OnMap && target is { OnMap: true }
            ? RangeFor(state)
                ?.Band(
                    Math.Sqrt(
                        Math.Pow(target.Column!.Value - actor.Column!.Value, 2)
                            + Math.Pow(target.Row!.Value - actor.Row!.Value, 2)
                    ) * 1.5
                )
            : null;
}
