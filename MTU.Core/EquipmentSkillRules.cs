using System.Text.RegularExpressions;

namespace MTU.Core;

public enum WeaponSkillMatch
{
    Exact,
    Basic,
    Unskilled,
}

public static class EquipmentSkillRules
{
    public static WeaponSkillMatch WeaponTraining(
        IEnumerable<CharacterSkill> skills,
        EquipmentItem weapon
    )
    {
        var required = weapon.RequiredSkill?.Trim();
        if (string.IsNullOrEmpty(required))
            return WeaponSkillMatch.Exact;
        var known = skills.ToArray();
        if (SkillRules.HasExactSkill(known, required))
            return WeaponSkillMatch.Exact;
        if (SkillRules.Rank(known, required) is null)
            return WeaponSkillMatch.Unskilled;
        return CharacterSkillCatalogue.Parse(required).Speciality is null
            ? WeaponSkillMatch.Exact
            : WeaponSkillMatch.Basic;
    }

    public static bool MeetsArmourRequirement(
        IEnumerable<CharacterSkill> skills,
        EquipmentItem armour
    )
    {
        var required = armour.RequiredSkill?.Trim() ?? "";
        if (
            required.Length == 0
            || required.Equals("None", StringComparison.OrdinalIgnoreCase)
            || required == "-"
        )
            return true;
        var match = Regex.Match(required, @"^(.*?)(?:\s+(\d+))?$", RegexOptions.CultureInvariant);
        var name = match.Groups[1].Value.Trim();
        var rank = match.Groups[2].Success
            ? int.TryParse(match.Groups[2].Value, out var parsed)
                ? parsed
                : int.MaxValue
            : 0;
        return SkillRules.Rank(skills, name) is int known && known >= rank;
    }

    public static bool LacksEquippedWeaponSkill(
        IEnumerable<CharacterSkill> skills,
        CharacterEquipmentDetails entry
    ) =>
        entry.Equipped
        && entry.Equipment.Kind == EquipmentKind.Weapon
        && WeaponTraining(skills, entry.Equipment) == WeaponSkillMatch.Unskilled;
}
