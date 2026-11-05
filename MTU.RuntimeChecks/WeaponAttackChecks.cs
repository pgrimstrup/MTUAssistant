using System.Text.Json;
using MTU.Core;

public static class WeaponAttackChecks
{
    public static void Run()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        static CharacterEquipmentDetails Weapon(
            string name,
            string damage,
            double range,
            string skill,
            bool equipped = true
        ) =>
            new()
            {
                Equipped = equipped,
                Equipment = new()
                {
                    Kind = EquipmentKind.Weapon,
                    Name = name,
                    Damage = damage,
                    Range = range,
                    RequiredSkill = skill,
                },
            };
        var pistol = Weapon("Pistol", "2D+2", 20, "Gun Combat (Slug)");
        var rifle = Weapon("Rifle", "3D", 100, "Gun Combat (Energy)");
        var knife = Weapon("Knife", "1D", 0, "Melee (Blade)");
        var club = Weapon("Club", "2D+1", 0, "Melee (Bludgeon)");
        var unequipped = Weapon("Cannon", "9DD", 500, "Gun Combat (Slug)", false);
        var loadout = new CombatLoadout([pistol, knife, rifle, club, unequipped]);
        Check(
            loadout.RangedWeapon == rifle && loadout.MeleeWeapon == club,
            "Strongest equipped weapons selected independently by attack type"
        );
        Check(
            CombatLoadout.AverageDamage("2d6 + 2") == 9
                && CombatLoadout.AverageDamage("3D") == 10.5
                && CombatLoadout.AverageDamage("1DD") == 35
                && CombatLoadout.AverageDamage("2D-1") == 6
                && CombatLoadout.AverageDamage("3D x 10") == 105
                && CombatLoadout.AverageDamage("4") == 4,
            "Damage comparisons support dice, modifiers, destructive dice, multipliers and flat damage"
        );
        Check(
            new CombatLoadout([knife, Weapon("Tie", "1D", 0, "Melee (Blade)")]).MeleeWeapon
                == knife,
            "Equal damage retains existing equipment order"
        );
        Check(
            new CombatLoadout([Weapon("Custom", "special", 0, "Melee (Blade)"), club]).MeleeWeapon
                == club,
            "Recognized damage sorts above custom text without blocking equipment"
        );
        var actor = new SceneCombatant(
            1,
            1,
            "Hero",
            false,
            false,
            true,
            0,
            0,
            "7C7777",
            Skills: [new(0, 1, "Gun Combat (Energy)", 3), new(0, 1, "Melee (Bludgeon)", 2)]
        );
        var target = new SceneCombatant(2, 2, "Foe", true, false, true, 1, 0, "777777");
        var roster = new[] { actor, target };
        var encounter = new Encounter();
        encounter.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
        encounter.ChooseAction(1, "Aim", roster);
        encounter.SetTarget(1, 2, roster);
        Check(
            AttackModifierCalculator
                .Calculate(encounter, actor, roster, loadout)!
                .Rows.Any(r => r.Name == "Gun Combat (Energy)" && r.Dm == 3),
            "Aim uses strongest firearm skill"
        );
        Check(
            CombatActionSummary
                .Create(encounter, actor, roster, loadout)!
                .Rows.Single(r => r.Label == "Weapon")
                .Value == "Rifle (100m, 3D)",
            "Aim summary identifies selected firearm"
        );
        encounter.ChooseAction(1, "Ranged Attack", roster, 2);
        Check(
            loadout.RangeFor(encounter.ActionFor(1))!.Range == 100,
            "Range uses selected firearm"
        );
        encounter.ResetActions(1, roster);
        encounter.ChooseAction(1, "Melee Attack", roster, 2);
        encounter.SetTarget(1, 2, roster);
        encounter.SetElevated(1, true, roster);
        // A committed target status applies to both kinds of attack.
        encounter.SetCoverStatus(2, true, false, roster);
        encounter.ResolveActions(2, roster);
        encounter.SetTargetReaction(1, "Dodge", roster);
        var melee = AttackModifierCalculator.Calculate(encounter, actor, roster, loadout)!;
        Check(
            melee.Rows.Any(r => r.Name == "Melee (Bludgeon)" && r.Dm == 2)
                && melee.Rows.Any(r => r.Name == "DEX" && r.Dm == 2)
                && melee.Rows.Any(r => r.Name == "Elevated" && r.Dm == 1)
                && melee.Rows.Any(r => r.Name == "Target behind cover" && r.Dm == -2)
                && melee.Rows.Any(r => r.Name == "Target Dodge" && r.Dm == -1)
                && melee.Total == 2
                && melee.CanResolve,
            "Melee uses selected weapon specialization and shared combat modifiers"
        );
        Check(
            !melee.Rows.Any(r =>
                r.Name.Contains("Range") || r.Name == "Aim" || r.Name.Contains("Sights")
            ),
            "Melee excludes firearm range, Aim and sight bonuses"
        );
        var summary = CombatActionSummary.Create(encounter, actor, roster, loadout, true)!;
        Check(
            summary.Rows.Single(r => r.Label == "Weapon").Value == "Club (Melee, 2D+1)"
                && summary.AttackModifiers!.Total == melee.Total,
            "GM and Cast summaries use selected melee weapon and total"
        );
        encounter.ResolveActions(1, roster, attackModifiers: melee);
        var restored = new Encounter();
        restored.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(encounter.Snapshot())
            )!,
            roster
        );
        Check(
            AttackModifierCalculator
                .Calculate(restored, actor, roster, new CombatLoadout([]))!
                .Rows.SequenceEqual(melee.Rows),
            "Resolved melee modifiers persist unchanged"
        );
        Check(
            pistol.Equipped
                && rifle.Equipped
                && knife.Equipped
                && club.Equipped
                && !unequipped.Equipped,
            "Weapon selection leaves GM equipment flags unchanged"
        );
        AttackModifierSummary ParryAgainst(
            SceneCombatant defender,
            string action = "Melee Attack",
            string reaction = "Parry"
        )
        {
            var parryRoster = new[] { actor, defender };
            var parryEncounter = new Encounter();
            parryEncounter.Begin(
                parryRoster,
                true,
                true,
                0,
                new Dictionary<int, int> { [1] = 20 },
                () => 7
            );
            parryEncounter.ChooseAction(1, action, parryRoster, 2);
            parryEncounter.SetTarget(1, 2, parryRoster);
            parryEncounter.SetTargetReaction(1, reaction, parryRoster);
            var result = AttackModifierCalculator.Calculate(
                parryEncounter,
                actor,
                parryRoster,
                loadout
            )!;
            var publicSummary = CombatActionSummary.Create(
                parryEncounter,
                actor,
                parryRoster,
                loadout,
                true
            )!;
            Check(
                publicSummary.AttackModifiers!.UnavailableReason == result.UnavailableReason
                    && publicSummary.AttackModifiers.Total == result.Total
                    && publicSummary.AttackModifiers.Rows.SequenceEqual(result.Rows),
                "Cast Parry breakdown matches GM calculation"
            );
            parryEncounter.ResolveActions(1, parryRoster, attackModifiers: result);
            var persisted = new Encounter();
            persisted.Restore(
                JsonSerializer.Deserialize<EncounterSnapshot>(
                    JsonSerializer.Serialize(parryEncounter.Snapshot())
                )!,
                parryRoster
            );
            Check(
                AttackModifierCalculator
                    .Calculate(persisted, actor, parryRoster, loadout)!
                    .Rows.SequenceEqual(result.Rows),
                "Parry modifier survives resolution and persistence"
            );
            return result;
        }
        var skilledParrier = target with
        {
            Skills =
            [
                new(0, 2, "Melee (Blade)", 2),
                new(0, 2, "Melee (Unarmed)", 4),
                new(0, 2, "Gun Combat (Slug)", 5),
            ],
        };
        Check(
            ParryAgainst(skilledParrier).Rows.Single(r => r.Name == "Target Parry").Dm == -4,
            "Adjacent melee Parry uses target's best specialization without target equipment"
        );
        Check(
            ParryAgainst(target with { Skills = [new(0, 2, "Melee (Natural)", 5)] })
                .Rows.Single(r => r.Name == "Target Parry")
                .Dm == -5,
            "Natural specialization counts for Parry even though excluded from weapon editing"
        );
        foreach (
            var skills in new IReadOnlyList<CharacterSkill>[]
            {
                [],
                [new(0, 2, "Melee", 0)],
                [new(0, 2, "Melee (Blade)", 0)],
                [new(0, 2, "Jack-of-All-Trades", 3)],
            }
        )
            Check(
                ParryAgainst(target with { Skills = skills })
                    .Rows.Single(r => r.Name == "Target Parry")
                    .Dm == -1,
                "Untrained or rank-zero Parry has minimum DM -1"
            );
        Check(
            ParryAgainst(skilledParrier with { Column = 1, Row = 1 })
                .Rows.Any(r => r.Name == "Target Parry"),
            "Diagonal neighbouring squares count as close combat"
        );
        Check(
            ParryAgainst(skilledParrier with { Column = 0, Row = 0 })
                .Rows.Any(r => r.Name == "Target Parry"),
            "Within 1.5m includes overlapping positions"
        );
        Check(
            !ParryAgainst(skilledParrier with { Column = 2, Row = 0 })
                .Rows.Any(r => r.Name == "Target Parry"),
            "Parry does not apply beyond close combat"
        );
        Check(
            !ParryAgainst(skilledParrier with { Column = null, Row = null })
                .Rows.Any(r => r.Name == "Target Parry"),
            "Unknown target position does not assume close combat"
        );
        Check(
            !ParryAgainst(skilledParrier, "Ranged Attack").Rows.Any(r => r.Name == "Target Parry"),
            "Parry does not penalize firearm attacks"
        );
        Check(
            !ParryAgainst(skilledParrier, reaction: "None").Rows.Any(r => r.Name == "Target Parry"),
            "Parry penalty requires selected Parry reaction"
        );
        Check(
            ParryAgainst(skilledParrier with { Column = 2, Row = 0 }).UnavailableReason
                == "Out of Range: target is not in Close Combat.",
            "Melee summary reports Out of Range beyond adjacent squares"
        );
        Check(
            ParryAgainst(skilledParrier with { Column = 1, Row = 1 }).UnavailableReason is null,
            "Melee range message clears in adjacent squares including diagonals"
        );
        Check(
            ParryAgainst(skilledParrier with { Column = null, Row = null }).UnavailableReason
                == "Place both tokens to determine close combat range.",
            "Unplaced tokens require positions instead of assuming Out of Range"
        );
        Encounter AttackFor(SceneCombatant attacker, string action = "Melee Attack")
        {
            var entries = new[] { attacker, target };
            var e = new Encounter();
            e.Begin(entries, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
            e.ChooseAction(1, action, entries, 2);
            e.SetTarget(1, 2, entries);
            return e;
        }
        var strong = actor with { Upp = "C77777" };
        var strongRoster = new[] { strong, target };
        var strongEncounter = AttackFor(strong);
        var strongModifiers = AttackModifierCalculator.Calculate(
            strongEncounter,
            strong,
            strongRoster,
            null
        )!;
        Check(
            strongModifiers.Rows.Any(r => r.Name == "STR" && r.Dm == 2)
                && !strongModifiers.Rows.Any(r => r.Name == "DEX"),
            "Melee skill uses and names STR when its DM is higher"
        );
        var unarmed = strongEncounter.BeginAttack(1, strongRoster, null, null, 0);
        Check(
            unarmed.Weapon is { Name: "Unarmed", Damage: "1D", RequiredSkill: "Melee (Unarmed)" }
                && unarmed.StrengthDamageDm == 2,
            "No melee equipment falls back to 1D Unarmed and captures STR damage DM"
        );
        unarmed.SetAttackRoll(8);
        unarmed.SetDamageRoll(4);
        Check(
            unarmed.Outcome!.TotalDamage == 4 + 2 + unarmed.Outcome.Effect
                && unarmed.Outcome.DamageSummary!.Contains("+ STR DM 2"),
            "Player melee damage includes visible STR DM before protection"
        );
        Check(
            CombatActionSummary
                .Create(strongEncounter, strong, strongRoster, null)!
                .Rows.Single(r => r.Label == "Weapon")
                .Value == "Unarmed (Melee, 1D)",
            "Unarmed fallback appears in Combat Summary"
        );
        var roundTrip = JsonSerializer.Deserialize<AttackOutcome>(
            JsonSerializer.Serialize(unarmed.Outcome)
        )!;
        Check(
            roundTrip.StrengthDamageDm == 2 && roundTrip.TotalDamage == unarmed.Outcome.TotalDamage,
            "Melee STR damage persists in saved attack outcome"
        );
        unarmed.AutoRoll(_ => 6);
        Check(
            unarmed.DamageRoll == 6
                && unarmed.Outcome!.TotalDamage == 6 + 2 + unarmed.Outcome.Effect,
            "Automatic unarmed attacks roll 1D and add STR DM"
        );
        strongEncounter.SetHp(1, 15, strongRoster);
        Check(
            strongEncounter.BeginAttack(1, strongRoster, null, null, 0).StrengthDamageDm == 0,
            "Melee damage uses current reduced STR rather than original STR"
        );
        var nimble = actor with { Upp = "4C7777" };
        var nimbleRoster = new[] { nimble, target };
        var nimbleEncounter = AttackFor(nimble);
        var nimbleAttack = nimbleEncounter.BeginAttack(
            1,
            nimbleRoster,
            new CombatLoadout([rifle]),
            null,
            0
        );
        Check(
            nimbleAttack.Weapon.RequiredSkill == "Melee (Unarmed)"
                && nimbleAttack.Modifiers.Rows.Any(r => r.Name == "DEX" && r.Dm == 2),
            "Firearm-only loadout falls back to Unarmed and uses higher DEX DM"
        );
        nimbleAttack.SetAttackRoll(8);
        nimbleAttack.SetDamageRoll(4);
        Check(
            nimbleAttack.StrengthDamageDm == -1
                && nimbleAttack.Outcome!.TotalDamage == 4 - 1 + nimbleAttack.Outcome.Effect
                && nimbleAttack.Outcome.DamageSummary!.Contains("- STR DM 1"),
            "Negative STR DM reduces melee damage and is labelled correctly"
        );
        Check(
            new AttackOutcome(8, 0, 1, 0, 0, StrengthDamageDm: -3).TotalDamage == 0,
            "Negative STR cannot create negative damage"
        );
        var grenade = Weapon("Grenade", "4D", 20, "Athletics (Dexterity)");
        var thrower = strong with { Skills = [new(0, 1, "Athletics (Dexterity)", 3)] };
        var thrownEncounter = AttackFor(thrower, "Ranged Attack");
        var thrown = thrownEncounter.BeginAttack(
            1,
            [thrower, target],
            new CombatLoadout([grenade]),
            null,
            0
        );
        Check(
            thrown.Modifiers.Rows.Any(r => r.Name == "Athletics (Dexterity)" && r.Dm == 3)
                && thrown.Modifiers.Rows.Any(r => r.Name == "DEX" && r.Dm == 0)
                && thrown.StrengthDamageDm is null,
            "Thrown Athletics weapons use Athletics/DEX and do not add melee STR damage"
        );
        Console.WriteLine(
            "Highest-damage weapon selection, melee modifiers, summaries and persistence passed."
        );
    }
}
