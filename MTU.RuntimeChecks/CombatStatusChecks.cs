using System.Text.Json;
using MTU.Core;

public static class CombatStatusChecks
{
    public static void Run()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        var actor = new SceneCombatant(1, 1, "Hero", false, false, true, 0, 0, "777777");
        var target = new SceneCombatant(2, 2, "Foe", true, false, true, 1, 0, "777777");
        var roster = new[] { actor, target };
        var loadout = new CombatLoadout([
            new()
            {
                Equipped = true,
                Equipment = new()
                {
                    Kind = EquipmentKind.Weapon,
                    Name = "Sword",
                    Damage = "2D",
                    RequiredSkill = "Melee (Blade)",
                },
            },
        ]);
        Encounter Fresh()
        {
            var e = new Encounter();
            e.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
            e.ChooseAction(1, "Melee Attack", roster, 2);
            e.SetTarget(1, 2, roster);
            return e;
        }
        var unconscious = Fresh();
        unconscious.SetUnconscious(2, true, roster);
        Check(
            unconscious.ActionFor(2) is { Unconscious: true, Stance: "Prone" },
            "Unconscious immediately sets Prone"
        );
        unconscious.ResetActions(2, roster);
        var restored = new Encounter();
        restored.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(unconscious.Snapshot())
            )!,
            roster
        );
        Check(
            restored.ActionFor(2).Unconscious,
            "Unconscious survives reset and encounter persistence"
        );
        restored.SetUnconscious(2, false, roster);
        Check(
            restored.ActionFor(2) is { Unconscious: false, Stance: "Prone" },
            "Clearing Unconscious preserves Prone"
        );
        foreach (
            var scenario in new[]
            {
                "Prone",
                "Pending Prone",
                "Dive for Prone",
                "Dive for Cover",
                "Prone then Cover Standing",
            }
        )
        {
            var e = Fresh();
            if (scenario is "Prone" or "Prone then Cover Standing")
                e.SetUnconscious(2, true, roster);
            if (scenario == "Pending Prone")
                e.SetStance(2, "Prone", roster);
            if (scenario == "Dive for Prone")
                e.SetTargetReaction(1, "Dive for Prone", roster);
            if (scenario is "Dive for Cover" or "Prone then Cover Standing")
            {
                e.SetTargetReaction(1, "Dive for Cover", roster);
                e.SetTargetReactionStance(
                    1,
                    scenario == "Dive for Cover" ? "Prone" : "Standing",
                    roster
                );
            }
            var rows = AttackModifierCalculator.Calculate(e, actor, roster, loadout)!.Rows;
            Check(
                rows.Count(r => r.Name == "Target prone") == 1
                    && rows.Single(r => r.Name == "Target prone").Dm == -1,
                "Target Prone applies once: " + scenario
            );
        }
        var dead = Fresh();
        dead.SetHp(2, 0, roster);
        var attack = dead.BeginAttack(1, roster, loadout, null, 0);
        attack.SetAttackRoll(12);
        attack.SetDamageRoll(12);
        dead.ApplyAttack(attack, roster);
        Check(
            dead.ActionFor(1).ActionsResolved && dead.Hp(target) == 0,
            "Dead target attack dialog and confirmation are allowed without reviving target"
        );
        Console.WriteLine(
            "Unconscious persistence/Prone, all Prone modifier paths and dead target attacks passed."
        );
    }
}
