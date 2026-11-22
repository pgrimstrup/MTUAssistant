using System.Globalization;
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
        var (name, rank) = ParseArmourRequirement(armour.RequiredSkill);
        return name.Length == 0 || SkillRules.Rank(skills, name) is int known && known >= rank;
    }

    // RULES-ASSUMPTION: the Core Rulebook only gates armour on Vacc Suit. The other skills are
    // a referee's pick of ones that plausibly govern worn gear (powered or heavy suits, grav
    // harnesses, diving and hostile-environment gear). Add or remove names here to change it.
    private static readonly string[] ArmourSkills =
    [
        "Vacc Suit",
        "Athletics",
        "Flyer",
        "Seafarer",
        "Survival",
    ];

    /// <summary>Skill names offered for an armour requirement, Vacc Suit first: each skill,
    /// followed by its specialities as "Skill (Speciality)".</summary>
    public static IReadOnlyList<string> ArmourSkillNames { get; } =
        ArmourSkills
            .Select(name => CharacterSkillCatalogue.Find(name)!)
            .SelectMany(skill =>
                skill.Specialities.Select(s => $"{skill.Name} ({s})").Prepend(skill.Name)
            )
            .ToArray();

    /// <summary>Splits an armour requirement such as "Vacc Suit 1" into skill and level.
    /// A missing level is 0. Blank, "None" and "-" mean no requirement (empty skill).</summary>
    public static (string Skill, int Level) ParseArmourRequirement(string? requirement)
    {
        var required = requirement?.Trim() ?? "";
        if (
            required.Length == 0
            || required.Equals("None", StringComparison.OrdinalIgnoreCase)
            || required == "-"
        )
            return ("", 0);
        var match = Regex.Match(required, @"^(.*?)(?:\s+(\d+))?$", RegexOptions.CultureInvariant);
        var level = match.Groups[2].Success
            ? int.TryParse(match.Groups[2].Value, out var parsed)
                ? parsed
                : int.MaxValue
            : 0;
        return (match.Groups[1].Value.Trim(), level);
    }

    /// <summary>Builds the stored armour requirement. Level 0 is assumed, so it is left out:
    /// "Gun Combat" rather than "Gun Combat 0". No skill gives null.</summary>
    public static string? FormatArmourRequirement(string? skill, int level)
    {
        var name = skill?.Trim() ?? "";
        if (name.Length == 0)
            return null;
        if (level < 0)
            throw new ArgumentException("Enter a skill level of zero or greater.");
        return level == 0 ? name : $"{name} {level.ToString(CultureInfo.InvariantCulture)}";
    }

    public static bool LacksEquippedWeaponSkill(
        IEnumerable<CharacterSkill> skills,
        CharacterEquipmentDetails entry
    ) =>
        entry.Equipped
        && entry.Equipment.Kind == EquipmentKind.Weapon
        && WeaponTraining(skills, entry.Equipment) == WeaponSkillMatch.Unskilled;
}
