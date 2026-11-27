using System.Text.RegularExpressions;

namespace MTU.Core;

/// <summary>One Injury Tolerance record: its effects (Halve Injury, Minus 1 per D) and the damage
/// types it applies to. A character or creature can have any number of these.</summary>
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
    Disruptor = 64,
}

/// <summary>The combined effect of every Injury Tolerance record that applies to one attack:
/// how many points to take off per damage die, and how many times to halve what is left.</summary>
public readonly record struct InjuryToleranceEffect(int MinusPerDie, int Halvings)
{
    public static InjuryToleranceEffect None => default;

    public bool Any => MinusPerDie > 0 || Halvings > 0;

    /// <summary>The effects of a single record, ignoring its damage types.</summary>
    public static InjuryToleranceEffect Of(InjuryTolerance record) =>
        new(
            record.HasFlag(InjuryTolerance.MinusOnePerDie) ? 1 : 0,
            record.HasFlag(InjuryTolerance.HalveInjury) ? 1 : 0
        );

    public static InjuryToleranceEffect operator +(
        InjuryToleranceEffect a,
        InjuryToleranceEffect b
    ) => new(a.MinusPerDie + b.MinusPerDie, a.Halvings + b.Halvings);
}

public static class InjuryToleranceRules
{
    /// <summary>More records than this on one character is almost certainly a mistake.</summary>
    public const int MaxRecords = 20;

    private const InjuryTolerance Effects =
        InjuryTolerance.HalveInjury | InjuryTolerance.MinusOnePerDie;
    private const InjuryTolerance Types =
        InjuryTolerance.Kinetic
        | InjuryTolerance.Energy
        | InjuryTolerance.Laser
        | InjuryTolerance.Disruptor
        | InjuryTolerance.Psionic;

    public static void Validate(InjuryTolerance value)
    {
        if ((value & ~(Effects | Types)) != 0)
            throw new ArgumentException("Choose valid Injury Tolerance options.");
    }

    public static void Validate(IReadOnlyList<InjuryTolerance>? records)
    {
        if ((records?.Count ?? 0) > MaxRecords)
            throw new ArgumentException(
                $"A character can have up to {MaxRecords} Injury Tolerance records."
            );
        foreach (var record in records ?? [])
            Validate(record);
    }

    /// <summary>The records to store: empty ones (nothing ticked) are dropped, order is kept.</summary>
    public static InjuryTolerance[] Normalize(IReadOnlyList<InjuryTolerance>? records) =>
        (records ?? []).Where(r => r != InjuryTolerance.None).ToArray();

    /// <summary>True when the record has at least one effect and one damage type, so it can
    /// ever change an attack's injury.</summary>
    public static bool IsComplete(InjuryTolerance record) =>
        (record & Effects) != 0 && (record & Types) != 0;

    /// <summary>Whether one record covers the attack. A record lists damage types; it applies
    /// when any of them matches the weapon.</summary>
    public static bool Applies(InjuryTolerance record, EquipmentItem weapon, bool melee)
    {
        var types = record & Types;
        if (types == InjuryTolerance.None)
            return false;
        if (melee)
            return types.HasFlag(InjuryTolerance.Kinetic);
        return (
                types.HasFlag(InjuryTolerance.Laser)
                && Regex.IsMatch(weapon.Name, @"\bLaser\b", RegexOptions.IgnoreCase)
            )
            || (
                types.HasFlag(InjuryTolerance.Disruptor)
                && Regex.IsMatch(weapon.Name, @"\bDisruptor\b", RegexOptions.IgnoreCase)
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
            );
    }

    /// <summary>The stacked effect of every record that covers the attack. A record counts once
    /// even when several of its damage types match.</summary>
    public static InjuryToleranceEffect Applicable(
        IEnumerable<InjuryTolerance>? records,
        EquipmentItem weapon,
        bool melee
    ) =>
        (records ?? [])
            .Where(record => Applies(record, weapon, melee))
            .Aggregate(
                InjuryToleranceEffect.None,
                (total, record) => total + InjuryToleranceEffect.Of(record)
            );

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

    // RULES-ASSUMPTION: stacked tolerances take off every per-die reduction first (two
    // "Minus 1 per D" records take 2 per die), then halve once for each halving record, rounding
    // down each time. This keeps a single record's order (subtract, then halve) unchanged.
    public static int Apply(int injury, InjuryToleranceEffect effect, int weaponDice)
    {
        injury = Math.Max(0, injury);
        if (effect.MinusPerDie > 0)
            injury = Math.Max(0, injury - effect.MinusPerDie * Math.Max(0, weaponDice));
        for (var i = 0; i < effect.Halvings && injury > 0; i++)
            injury /= 2;
        return injury;
    }

    public static string Describe(InjuryToleranceEffect effect, int weaponDice) =>
        string.Join(
            ", ",
            new[]
            {
                effect.MinusPerDie > 0
                    ? $"Minus {effect.MinusPerDie} per D ({weaponDice} dice)"
                    : null,
                effect.Halvings switch
                {
                    0 => null,
                    1 => "Halve Injury",
                    var n => $"Halve Injury ×{n}",
                },
            }.Where(text => text is not null)
        );

    /// <summary>A short label for one record, such as "Halve Injury vs Kinetic, Laser".</summary>
    public static string Label(InjuryTolerance record)
    {
        var effects = new[]
        {
            record.HasFlag(InjuryTolerance.HalveInjury) ? "Halve Injury" : null,
            record.HasFlag(InjuryTolerance.MinusOnePerDie) ? "Minus 1 per D" : null,
        }.OfType<string>();
        var types = new[]
        {
            (InjuryTolerance.Kinetic, "Kinetic"),
            (InjuryTolerance.Energy, "Energy"),
            (InjuryTolerance.Laser, "Laser"),
            (InjuryTolerance.Disruptor, "Disruptor"),
            (InjuryTolerance.Psionic, "Psionic"),
        }
            .Where(t => record.HasFlag(t.Item1))
            .Select(t => t.Item2);
        var effectText = string.Join(" + ", effects);
        var typeText = string.Join(", ", types);
        return (effectText.Length, typeText.Length) switch
        {
            (0, 0) => "Empty",
            (0, _) => $"No effect vs {typeText}",
            (_, 0) => $"{effectText} (no damage type)",
            _ => $"{effectText} vs {typeText}",
        };
    }
}
