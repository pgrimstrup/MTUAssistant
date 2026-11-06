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

public sealed record ProtectionContribution(int Value, string? Label = null)
{
    public string Description => $"DR +{Value}" + (Label is null ? "" : $" {Label}");
}

public sealed class CombatLoadout(
    IReadOnlyList<CharacterEquipmentDetails> equipment,
    bool naturalWeapons = false
)
{
    private IEnumerable<CharacterEquipmentDetails> Equipped => equipment.Where(e => e.Equipped);
    public int AutoScore => AutoFire.Score(RangedWeapon?.Equipment.Traits);
    public bool HasSmartTracker => Equipped.Any(e => e.SupportsAttachments && e.SmartTracker);
    public CharacterEquipmentDetails? RangedWeapon => StrongestWeapon(true);
    public IReadOnlyList<CharacterEquipmentDetails> NaturalWeapons =>
        naturalWeapons
            ? Equipped.Where(e => e.Equipment.Kind == EquipmentKind.Weapon).ToArray()
            : [];
    public CharacterEquipmentDetails MeleeWeapon => StrongestWeapon(false) ?? UnarmedWeapon;
    public static CharacterEquipmentDetails UnarmedWeapon =>
        new()
        {
            Equipped = true,
            Equipment = new()
            {
                Kind = EquipmentKind.Weapon,
                Name = "Unarmed",
                RequiredSkill = "Melee (Unarmed)",
                Damage = "1D",
            },
        };

    private CharacterEquipmentDetails? StrongestWeapon(bool ranged) =>
        Equipped
            .Where(e =>
                e.Equipment.Kind == EquipmentKind.Weapon && (e.Equipment.Range > 0) == ranged
            )
            .OrderByDescending(e => AverageDamage(e.Equipment.Damage))
            .FirstOrDefault();

    // Compare expected damage; equal or unrecognised expressions retain catalog order.
    public static double AverageDamage(string damage)
    {
        var text = Regex.Replace(damage ?? "", @"\s+", "").ToUpperInvariant().Replace('−', '-');
        var match = Regex.Match(
            text,
            @"^(?<dice>\d*)D(?<sides>\d+|D)?(?<bonus>[+-]\d+)?(?:(?:X|×|\*)(?<multiplier>\d+))?$"
        );
        if (!match.Success)
            return
                double.TryParse(
                    text,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var flat
                ) && double.IsFinite(flat)
                ? flat
                : -1;
        double Number(string group, double fallback) =>
            double.TryParse(
                match.Groups[group].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var number
            )
                ? number
                : fallback;
        var destructive = match.Groups["sides"].Value == "D";
        var average =
            (Number("dice", 1) * (Number("sides", 6) + 1) / 2 + Number("bonus", 0))
            * Number("multiplier", 1)
            * (destructive ? 10 : 1);
        return double.IsFinite(average) ? average : -1;
    }

    public CharacterEquipmentDetails? WeaponFor(CombatantActionState state) =>
        state.MajorAction == "Melee Attack"
            ? (
                naturalWeapons
                    ? NaturalWeapons.FirstOrDefault(w => w.Equipment.Id == state.NaturalAttackId)
                        ?? NaturalWeapons.FirstOrDefault()
                    : MeleeWeapon
            )
        : state.MajorAction == "Ranged Attack" || state.IsSelected("Aim") ? RangedWeapon
        : null;

    public string WeaponsFor(CombatantActionState state) =>
        state.MajorAction is "Melee Attack" or "Ranged Attack" || state.IsSelected("Aim")
            ? WeaponFor(state) is { } weapon
                ? DescribeWeapon(weapon)
                : ""
            : Weapons;

    private static string DescribeWeapon(CharacterEquipmentDetails weapon) =>
        $"{weapon.Equipment.Name} ({(weapon.Equipment.Range > 0 ? $"{weapon.Equipment.Range:0.##}m" : "Melee")}, {weapon.Equipment.Damage})";

    public int ArmourProtection =>
        Equipped
            .Where(e => e.Equipment.Kind == EquipmentKind.Armour)
            .Sum(e => e.Equipment.Protection);

    public int ArmourProtectionFor(EquipmentItem weapon, bool melee) =>
        ArmourProtectionPartsFor(weapon, melee).Sum(part => part.Value);

    public IReadOnlyList<ProtectionContribution> ArmourProtectionPartsFor(
        EquipmentItem weapon,
        bool melee
    )
    {
        var laser = Regex.IsMatch(weapon.Name, @"\bLaser\b", RegexOptions.IgnoreCase);
        var energy = string.Equals(
            weapon.RequiredSkill?.Trim(),
            "Gun Combat (Energy)",
            StringComparison.OrdinalIgnoreCase
        );
        var slug = string.Equals(
            weapon.RequiredSkill?.Trim(),
            "Gun Combat (Slug)",
            StringComparison.OrdinalIgnoreCase
        );
        return Equipped
            .Where(e => e.Equipment.Kind == EquipmentKind.Armour)
            .Select(e =>
            {
                var armour = e.Equipment;
                if (melee)
                    return armour.ProtectionK is int meleeKinetic
                        ? new ProtectionContribution(meleeKinetic, "vs Kinetic")
                        : new(armour.Protection);
                if (laser && armour.ProtectionL is int laserProtection)
                    return new(laserProtection, "vs Lasers");
                if (energy && armour.ProtectionE is int energyProtection)
                    return new(energyProtection, "vs Energy");
                if (slug && armour.ProtectionK is int kineticProtection)
                    return new(kineticProtection, "vs Kinetic");
                return new ProtectionContribution(armour.Protection);
            })
            .ToArray();
    }

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
            Equipped.Where(e => e.Equipment.Kind == EquipmentKind.Weapon).Select(DescribeWeapon)
        );

    public CombatWeaponRange? RangeFor(CombatantActionState state)
    {
        var weapon = RangedWeapon;
        return weapon is null
            ? null
            : new(
                weapon.Equipment.Range,
                !state.AutomaticFire
                    && (
                        weapon.Scope
                        || Regex.IsMatch(
                            weapon.Equipment.Traits,
                            @"\bScope\b",
                            RegexOptions.IgnoreCase
                        )
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
