using System.Globalization;
using System.Text.RegularExpressions;

namespace MTU.Core;

public static class AnimalCombat
{
    public static int Trait(string traits, string name)
    {
        var match = Regex.Match(
            traits ?? "",
            @"(?:^|[,;\r\n]|\s)"
                + Regex.Escape(name)
                + @"\s*(?:\(\s*|:\s*|=\s*)?(?<score>[+-]?\d+)\b",
            RegexOptions.IgnoreCase
        );
        return
            match.Success
            && int.TryParse(
                match.Groups["score"].Value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var score
            )
            ? name.Equals("Small", StringComparison.OrdinalIgnoreCase)
                ? (score == int.MinValue ? 0 : Math.Abs(score))
                : Math.Max(0, score)
            : 0;
    }

    public static int InitiativeDm(SceneCombatant animal) =>
        animal.IsAnimal
            ? Trait(animal.AnimalTraits, "Fast Metabolism")
                - Trait(animal.AnimalTraits, "Slow Metabolism")
            : 0;

    public static int Speed(SceneCombatant animal, bool flying) =>
        animal.Speed + (animal.IsAnimal && flying ? Trait(animal.AnimalTraits, "Flyer") : 0);

    public static int? TacticsRank(IEnumerable<SceneCombatant> animals) =>
        animals
            .SelectMany(e => e.Skills ?? [])
            .Where(s =>
                s.Name.Equals("Tactics", StringComparison.OrdinalIgnoreCase)
                || s.Name.StartsWith("Tactics (", StringComparison.OrdinalIgnoreCase)
            )
            .Select(s => (int?)s.Level)
            .Max();

    public static bool InMeleeReach(SceneCombatant actor, SceneCombatant target)
    {
        if (!actor.OnMap || !target.OnMap)
            return false;
        var columns = Math.Abs(actor.Column!.Value - target.Column!.Value);
        var rows = Math.Abs(actor.Row!.Value - target.Row!.Value);
        if (columns <= 1 && rows <= 1)
            return true;
        var distance = Math.Sqrt((double)columns * columns + (double)rows * rows) * 1.5;
        return distance <= 1.5 + (actor.IsAnimal ? Trait(actor.AnimalTraits, "Long Reach") : 0);
    }

    public static CombatLoadout Loadout(SceneCombatant animal) =>
        new(
            (animal.NaturalAttacks ?? [])
                .Select(
                    (a, index) =>
                        new CharacterEquipmentDetails
                        {
                            Equipped = true,
                            Equipment = new EquipmentItem
                            {
                                Id = index + 1,
                                Kind = EquipmentKind.Weapon,
                                Name = a.Name,
                                RequiredSkill = "Melee (Natural)",
                                Damage = a.Damage,
                            },
                        }
                )
                .Concat(
                    Trait(animal.AnimalTraits, "Armour") > 0
                        ? new[]
                        {
                            new CharacterEquipmentDetails
                            {
                                Equipped = true,
                                Equipment = new EquipmentItem
                                {
                                    Kind = EquipmentKind.Armour,
                                    Name = "Natural Armour",
                                    Protection = Trait(animal.AnimalTraits, "Armour"),
                                },
                            },
                        }
                        : []
                )
                .ToArray(),
            naturalWeapons: true
        );
}
