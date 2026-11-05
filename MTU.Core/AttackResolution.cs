using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MTU.Core;

public sealed record CombatMishap(int Roll, int SkillDm, int IntelligenceDm)
{
    [JsonIgnore]
    public int Total => Roll + SkillDm + IntelligenceDm;

    [JsonIgnore]
    public string Description =>
        Total switch
        {
            <= 2 => "Self-hit for normal weapon damage.",
            <= 4 =>
                "Weapon dropped out of reach; unavailable for this fight and lost if forced to flee.",
            <= 6 => "Weapon dropped in sight; recover next round with a significant action.",
            7 =>
                "Minor weapon or grip problem; attack wasted. Restore use next round with a significant action and an Easy (6+) skill check; retry each round if unsuccessful.",
            <= 9 => "Severe weapon malfunction; workshop repairs required.",
            <= 11 => "Weapon destroyed by an ammunition explosion or serious structural damage.",
            _ => "Attack missed; no further mishap.",
        };

    [JsonIgnore]
    public string Summary =>
        $"Mishap check: rolled {Roll}, skill DM {SkillDm:+0;-0;0}, INT DM {IntelligenceDm:+0;-0;0} → {Total}. {Description}";
}

public sealed record AttackOutcome(
    int AttackRoll,
    int Modifier,
    int? DamageRoll,
    int OverallProtection,
    int ArmourPiercing,
    int? AppliedInjury = null,
    CombatMishap? Mishap = null
)
{
    [JsonIgnore]
    public int Effect => AttackRoll + Modifier - 8;

    [JsonIgnore]
    public bool Hit => AttackRoll != 2 && Effect >= 0;

    [JsonIgnore]
    public bool IsMishap => AttackRoll == 2 && Modifier < 6;

    [JsonIgnore]
    public int TotalDamage => Hit && DamageRoll.HasValue ? DamageRoll.Value + Effect : 0;

    [JsonIgnore]
    public int RemainingProtection => Math.Max(0, OverallProtection - ArmourPiercing);

    [JsonIgnore]
    public int CalculatedInjury => Math.Max(0, TotalDamage - RemainingProtection);

    [JsonIgnore]
    public string AttackSummary =>
        $"Rolled {AttackRoll} {(Modifier < 0 ? "-" : "+")} {Math.Abs(Modifier)} → {AttackRoll + Modifier} is a "
        + (
            IsMishap ? "Mishap"
            : !Hit ? "Miss"
            : $"Hit (Effect {Effect})"
        )
        + (Mishap is { } mishap ? $". {mishap.Summary}" : "");

    [JsonIgnore]
    public string? DamageSummary =>
        !Hit ? null
        : !DamageRoll.HasValue ? "Pending damage roll."
        : $"Rolled {DamageRoll} + Effect {Effect} → {TotalDamage} damage"
            + (AppliedInjury.HasValue ? $". {AppliedInjury} injury applied." : "");

    [JsonIgnore]
    public string Summary =>
        AttackSummary + (Hit && DamageRoll.HasValue ? ". " + DamageSummary : "");
}

public sealed class AttackResolution
{
    public int ActorId { get; }
    public int TargetId { get; }
    public string ActorName { get; }
    public string TargetName { get; }
    public EquipmentItem Weapon { get; }
    public AttackModifierSummary Modifiers { get; }
    public int OverallProtection { get; }
    public int ArmourPiercing { get; }
    public int TargetOriginalHp { get; }
    public int TargetRemainingHp { get; }
    public string? CalculatedInjuryColour
    {
        get
        {
            var outcome = Outcome;
            if (outcome is null || (outcome.Hit && !DamageRoll.HasValue))
                return null;
            if (!outcome.Hit || outcome.CalculatedInjury == 0)
                return "LimeGreen";
            if (outcome.CalculatedInjury > TargetRemainingHp)
                return "#ff8080";
            if (outcome.CalculatedInjury > TargetOriginalHp * .66m)
                return "Orange";
            if (outcome.CalculatedInjury > TargetOriginalHp * .33m)
                return "Yellow";
            return null;
        }
    }
    public int? AttackRoll { get; private set; }
    public int? DamageRoll { get; private set; }
    public int? Injury { get; private set; }
    public bool NpcMode { get; private set; }
    public string? RollError { get; private set; }
    public CombatMishap? Mishap { get; private set; }
    private readonly int mishapSkillDm;
    private readonly int mishapIntelligenceDm;
    public AttackOutcome? Outcome =>
        AttackRoll is int roll
            ? new(
                roll,
                Modifiers.Total,
                DamageRoll,
                OverallProtection,
                ArmourPiercing,
                Mishap: Mishap
            )
            : null;
    public bool Ready =>
        Outcome is { } result && (!result.Hit || DamageRoll.HasValue) && Injury is >= 0;

    internal AttackResolution(
        SceneCombatant actor,
        SceneCombatant target,
        EquipmentItem weapon,
        AttackModifierSummary modifiers,
        int protection,
        int targetOriginalHp,
        int targetRemainingHp
    )
    {
        ActorId = actor.Id;
        TargetId = target.Id;
        TargetOriginalHp = targetOriginalHp;
        TargetRemainingHp = targetRemainingHp;
        ActorName = actor.Name;
        TargetName = target.Name;
        Weapon = weapon.Copy();
        Modifiers = modifiers;
        mishapSkillDm = string.IsNullOrWhiteSpace(weapon.RequiredSkill)
            ? 0
            : SkillRules.Dm(actor.Skills ?? [], weapon.RequiredSkill);
        mishapIntelligenceDm = Encounter.Dm(Encounter.Stat(actor, 3));
        OverallProtection = Math.Max(0, protection);
        var ap = Regex.Match(
            weapon.Traits,
            @"\bAP\s*(?:\(\s*|[:=]\s*)?(?<rating>\d+)\b",
            RegexOptions.IgnoreCase
        );
        ArmourPiercing = int.TryParse(ap.Groups["rating"].Value, out var rating) ? rating : 0;
        SetMode(actor.IsNpc);
    }

    public void SetMode(bool npc, Func<int, int>? die = null)
    {
        NpcMode = npc;
        AttackRoll = DamageRoll = Injury = null;
        Mishap = null;
        RollError = null;
        if (npc)
            AutoRoll(die);
    }

    public void AutoRoll(Func<int, int>? die = null)
    {
        AttackRoll = DamageRoll = Injury = null;
        Mishap = null;
        RollError = null;
        die ??= sides => Random.Shared.Next(1, sides + 1);
        SetAttackRoll(die(6) + die(6), die);
        if (Outcome?.Hit != true)
            return;
        if (TryRollDamage(Weapon.Damage, die, out var damage))
            SetDamageRoll(damage);
        else
            RollError =
                $"Cannot roll damage expression '{Weapon.Damage}' automatically. Enter the damage roll manually.";
    }

    public void SetAttackRoll(int? roll, Func<int, int>? die = null)
    {
        if (roll.HasValue && roll is < 2 or > 12)
            throw new ArgumentException("Enter an attack roll from 2 to 12.");
        AttackRoll = roll;
        DamageRoll = null;
        Mishap = null;
        if (roll == 2 && Modifiers.Total < 6)
        {
            die ??= sides => Random.Shared.Next(1, sides + 1);
            var mishapRoll = die(6) + die(6);
            if (mishapRoll is < 2 or > 12)
                throw new ArgumentException("Mishap dice must produce a 2D6 roll.");
            Mishap = new(mishapRoll, mishapSkillDm, mishapIntelligenceDm);
        }
        Injury = Outcome?.Hit == false ? 0 : null;
    }

    public void SetDamageRoll(int? roll)
    {
        if (Outcome?.Hit != true)
            throw new ArgumentException("Roll a hit before entering damage.");
        if (roll is < 0 or > 1_000_000)
            throw new ArgumentException("Enter a nonnegative damage roll up to 1,000,000.");
        DamageRoll = roll;
        Injury = roll.HasValue ? Outcome!.CalculatedInjury : null;
    }

    public void SetInjury(int? injury)
    {
        if (injury is < 0 or > 1_000_000)
            throw new ArgumentException("Enter a nonnegative injury up to 1,000,000.");
        Injury = injury;
    }

    public static bool TryRollDamage(string expression, Func<int, int> die, out int damage)
    {
        damage = 0;
        var text = Regex.Replace(expression ?? "", @"\s+", "").ToUpperInvariant().Replace('−', '-');
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out damage))
            return damage is >= 0 and <= 1_000_000;
        var match = Regex.Match(
            text,
            @"^(?<dice>\d*)D(?<sides>\d+|D)?(?<bonus>[+-]\d+)?(?:(?:X|×|\*)(?<multiplier>\d+))?$"
        );
        if (!match.Success)
            return false;
        int Number(string group, int fallback) =>
            match.Groups[group].Length == 0
                ? fallback
                : int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
        try
        {
            var dice = Number("dice", 1);
            var destructive = match.Groups["sides"].Value == "D";
            var sides = destructive ? 6 : Number("sides", 6);
            if (dice is < 1 or > 1000 || sides is < 1 or > 1000)
                return false;
            var total = 0;
            for (var i = 0; i < dice; i++)
            {
                var value = die(sides);
                if (value < 1 || value > sides)
                    throw new ArgumentException("Die result is outside its range.");
                total = checked(total + value);
            }
            damage = Math.Max(
                0,
                checked(
                    (total + Number("bonus", 0)) * Number("multiplier", 1) * (destructive ? 10 : 1)
                )
            );
            return damage <= 1_000_000;
        }
        catch (Exception ex) when (ex is OverflowException or FormatException)
        {
            return false;
        }
    }
}

public sealed partial class Encounter
{
    public AttackResolution BeginAttack(
        int id,
        IReadOnlyList<SceneCombatant> entries,
        CombatLoadout? loadout,
        CombatLoadout? targetLoadout,
        int naturalDr
    )
    {
        var actor = RequireActor(id, entries);
        RequireUnresolved(id);
        var state = ActionFor(id);
        if (state.MajorAction is not ("Ranged Attack" or "Melee Attack"))
            throw new ArgumentException("Choose an attack first.");
        var target = entries.FirstOrDefault(e => e.Id == state.TargetId && e.IsNpc != actor.IsNpc);
        if (target is null)
            throw new ArgumentException("Choose a target before resolving the attack.");
        var modifiers = AttackModifierCalculator.Calculate(this, actor, entries, loadout)!;
        if (!modifiers.CanResolve)
            throw new ArgumentException(modifiers.UnavailableReason);
        if (state.SelectedHiding)
            throw new ArgumentException("Stop Hiding before making an attack.");
        var weapon =
            loadout?.WeaponFor(state)
            ?? throw new ArgumentException("Choose an equipped weapon for the attack.");
        var defence = ActionFor(target.Id);
        var protection =
            (
                targetLoadout?.ArmourProtectionFor(
                    weapon.Equipment,
                    state.MajorAction == "Melee Attack"
                ) ?? 0
            )
            + Math.Max(0, naturalDr)
            + (defence.Hiding ? defence.CoverProtection?.Protection ?? 0 : 0);
        return new(
            actor,
            target,
            weapon.Equipment,
            modifiers,
            protection,
            MaximumHp(target),
            Hp(target)
        );
    }

    // Previews are transient until the GM confirms, and are cleared on cancellation.
    public void PreviewAttack(int id, AttackOutcome? outcome)
    {
        RequireUnresolved(id);
        actions[id] = ActionFor(id) with { AttackOutcome = outcome };
    }

    public void ApplyAttack(
        AttackResolution attack,
        IReadOnlyList<SceneCombatant> entries,
        bool smartTracker = false
    )
    {
        RequireActor(attack.ActorId, entries);
        RequireUnresolved(attack.ActorId);
        var state = ActionFor(attack.ActorId);
        if (
            state.TargetId != attack.TargetId
            || state.MajorAction is not ("Ranged Attack" or "Melee Attack")
        )
            throw new ArgumentException(
                "The selected attack has changed. Cancel and resolve it again."
            );
        if (!attack.Ready)
            throw new ArgumentException(
                "Complete the attack and damage rolls before applying damage."
            );
        var target =
            entries.FirstOrDefault(e => e.Id == attack.TargetId)
            ?? throw new ArgumentException("The target is no longer in this scene.");
        var result = attack.Outcome! with
        {
            AppliedInjury = attack.Outcome!.Hit ? attack.Injury : 0,
        };
        ResolveActions(attack.ActorId, entries, smartTracker, attack.Modifiers);
        actions[attack.ActorId] = ActionFor(attack.ActorId) with
        {
            AttackOutcome = result,
            TargetHasBeenHit = state.TargetHasBeenHit || result.Hit,
        };
        if (result.Hit)
            SetHp(target.Id, Math.Max(0, Hp(target) - result.AppliedInjury!.Value), entries);
    }
}
