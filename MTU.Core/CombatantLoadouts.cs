using System.Text.RegularExpressions;

namespace MTU.Core;

public static class CombatantLoadouts
{
    public static IReadOnlyList<EquipmentItem> Select(Npc npc, IEnumerable<EquipmentItem> catalogue)
    {
        if (npc.Type != NpcType.Combatant)
            return [];
        var items = catalogue.Where(e => e.Id > 0).ToArray();
        var result = new List<EquipmentItem>();
        var armour = items
            .Where(e =>
                e.Kind == EquipmentKind.Armour
                && e.Protection > 0
                && !e.Name.Contains("battle dress", StringComparison.OrdinalIgnoreCase)
                && CanWear(npc.Character, e)
            )
            .OrderBy(e =>
                Preference(e.Name, ["cloth", "flak", "ballistic", "combat armour", "mesh", "jack"])
            )
            .ThenBy(e => e.Tl)
            .ThenByDescending(e => e.Protection)
            .ThenBy(e => e.Cost)
            .ThenBy(e => e.Name)
            .ThenBy(e => e.Tl)
            .ThenBy(e => e.Id)
            .FirstOrDefault();
        if (armour is not null)
            result.Add(armour);
        var ranged = items
            .Where(e =>
                e.Kind == EquipmentKind.Weapon
                && e.Range > 0
                && !new[]
                {
                    "launcher",
                    "grenade",
                    "cannon",
                    "mortar",
                    "artillery",
                    "howitzer",
                    "heavy machine",
                    "flame",
                }.Any(word => e.Name.Contains(word, StringComparison.OrdinalIgnoreCase))
            )
            .OrderByDescending(e => WeaponSkill(npc.Character, e))
            .ThenBy(e =>
                Preference(
                    e.Name,
                    [
                        "assault rifle",
                        "combat rifle",
                        "gauss rifle",
                        "rifle",
                        "carbine",
                        "submachine",
                        "pistol",
                    ]
                )
            )
            .ThenBy(e => e.Cost)
            .ThenBy(e => e.Name)
            .ThenBy(e => e.Tl)
            .ThenBy(e => e.Id)
            .FirstOrDefault();
        if (ranged is not null)
            result.Add(ranged);
        var melee = items
            .Where(e =>
                e.Kind == EquipmentKind.Weapon
                && e.Range == 0
                && !new[] { "claw", "bite", "fist", "unarmed", "natural" }.Any(word =>
                    e.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                )
            )
            .OrderBy(e => Preference(e.Name, ["knife", "dagger", "blade", "sword"]))
            .ThenBy(e => e.Cost)
            .ThenBy(e => e.Name)
            .ThenBy(e => e.Tl)
            .ThenBy(e => e.Id)
            .FirstOrDefault();
        if (melee is not null)
            result.Add(melee);
        return result;
    }

    private static int Preference(string name, string[] preferred)
    {
        var index = Array.FindIndex(
            preferred,
            word => name.Contains(word, StringComparison.OrdinalIgnoreCase)
        );
        return index < 0 ? preferred.Length : index;
    }

    private static bool CanWear(Character npc, EquipmentItem armour)
    {
        var required = armour.RequiredSkill.Trim();
        if (
            required.Length == 0
            || required.Equals("None", StringComparison.OrdinalIgnoreCase)
            || required == "-"
        )
            return true;
        var match = Regex.Match(required, @"^(.*?)(?:\s+(\d+))?$", RegexOptions.CultureInvariant);
        var skill = match.Groups[1].Value.Trim();
        var rank = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        return SkillRules.Rank(npc.Skills ?? [], skill) is int known && known >= rank;
    }

    private static int WeaponSkill(Character npc, EquipmentItem weapon)
    {
        var name = weapon.Name;
        var speciality =
            new[] { "laser", "plasma", "fusion", "stunner" }.Any(s =>
                name.Contains(s, StringComparison.OrdinalIgnoreCase)
            )
                ? "Energy"
            : name.Contains("bow", StringComparison.OrdinalIgnoreCase) ? "Archaic"
            : "Slug";
        return CharacterService.SkillDm(npc, $"Gun Combat ({speciality})");
    }
}
