using System.Text.RegularExpressions;

namespace MTU.Core;

[Flags]
public enum InjuryTolerance
{
    None = 0,
    HalveInjury = 1,
    MinusOnePerDie = 2,
    Kinetic = 4,
    Energy = 8,
    Laser = 16,
    Psionic = 32,
}

public static class InjuryToleranceRules
{
    private const InjuryTolerance Effects =
        InjuryTolerance.HalveInjury | InjuryTolerance.MinusOnePerDie;
    private const InjuryTolerance Types =
        InjuryTolerance.Kinetic
        | InjuryTolerance.Energy
        | InjuryTolerance.Laser
        | InjuryTolerance.Psionic;

    public static void Validate(InjuryTolerance value)
    {
        if ((value & ~(Effects | Types)) != 0)
            throw new ArgumentException("Choose valid Injury Tolerance options.");
    }

    public static InjuryTolerance Applicable(
        InjuryTolerance tolerance,
        EquipmentItem weapon,
        bool melee
    )
    {
        var types = tolerance & Types;
        var applies =
            types != InjuryTolerance.None
            && (
                melee
                    ? types.HasFlag(InjuryTolerance.Kinetic)
                    : (
                        types.HasFlag(InjuryTolerance.Laser)
                        && Regex.IsMatch(weapon.Name, @"\bLaser\b", RegexOptions.IgnoreCase)
                    )
                        || (
                            types.HasFlag(InjuryTolerance.Energy)
                            && string.Equals(
                                weapon.RequiredSkill?.Trim(),
                                "Gun Combat (Energy)",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        || (
                            types.HasFlag(InjuryTolerance.Kinetic)
                            && string.Equals(
                                weapon.RequiredSkill?.Trim(),
                                "Gun Combat (Slug)",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
            );
        return applies ? tolerance & Effects : InjuryTolerance.None;
    }

    public static int DamageDice(string expression)
    {
        var text = Regex.Replace(expression ?? "", @"\s+", "");
        var match = Regex.Match(
            text,
            @"^(?<dice>\d*)D(?:\d+|D)?(?:[+-]\d+)?(?:(?:X|×|\*)\d+)?$",
            RegexOptions.IgnoreCase
        );
        if (!match.Success)
            return 0;
        return match.Groups["dice"].Length == 0 ? 1
            : int.TryParse(match.Groups["dice"].Value, out var dice) ? Math.Max(0, dice)
            : 0;
    }

    public static int Apply(int injury, InjuryTolerance effects, int weaponDice)
    {
        injury = Math.Max(0, injury);
        if (effects.HasFlag(InjuryTolerance.MinusOnePerDie))
            injury = Math.Max(0, injury - Math.Max(0, weaponDice));
        if (effects.HasFlag(InjuryTolerance.HalveInjury))
            injury /= 2;
        return injury;
    }

    public static string Describe(InjuryTolerance effects, int weaponDice) =>
        string.Join(
            ", ",
            new[]
            {
                effects.HasFlag(InjuryTolerance.MinusOnePerDie)
                    ? $"Minus 1 per D ({weaponDice} dice)"
                    : null,
                effects.HasFlag(InjuryTolerance.HalveInjury) ? "Halve Injury" : null,
            }.Where(text => text is not null)
        );
}
