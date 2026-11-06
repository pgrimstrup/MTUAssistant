using System.Text.Json;
using MTU.Core;

public static class AttackModifierChecks
{
    public static void Run()
    {
        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
        var actor = new SceneCombatant(
            1,
            1,
            "Shooter",
            false,
            false,
            true,
            0,
            0,
            "7C7777",
            Skills: [new(0, 1, "Gun Combat (Slug)", 2)]
        );
        var target = new SceneCombatant(
            2,
            2,
            "Target",
            true,
            false,
            true,
            10,
            0,
            "7B7777",
            Skills: [new(0, 2, "Athletics (Dexterity)", 3)]
        );
        var roster = new[] { actor, target };
        var weapon = new CharacterEquipmentDetails
        {
            Equipped = true,
            LaserSight = true,
            HolographicSight = true,
            Phud = true,
            SmartTracker = true,
            XRayOutliningAid = true,
            Equipment = new()
            {
                Kind = EquipmentKind.Weapon,
                Name = "Rifle",
                Range = 100,
                RequiredSkill = "Gun Combat (Slug)",
            },
        };
        var loadout = new CombatLoadout([weapon]);
        Encounter Fresh()
        {
            var encounter = new Encounter();
            encounter.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
            encounter.ChooseAction(1, "Ranged Attack", roster);
            encounter.SetTarget(1, 2, roster);
            return encounter;
        }
        int Dm(AttackModifierSummary summary, string name) =>
            summary.Rows.FirstOrDefault(r => r.Name == name)?.Dm ?? 0;
        foreach (var moveFirst in new[] { false, true })
        {
            var ordered = Fresh();
            ordered.ChooseAction(1, "Move", roster, moveFirst ? 1 : 3);
            var movedActor = actor with { Column = 200, Row = 0 };
            var movedRoster = new[] { movedActor, target };
            var summary = AttackModifierCalculator.Calculate(
                ordered,
                movedActor,
                movedRoster,
                loadout
            )!;
            Check(
                loadout.TargetBand(ordered.ActionFor(1), movedActor, target)
                    == (moveFirst ? "Extreme Range" : "Short Range"),
                "Attack range follows ordered movement"
            );
            Check(
                Dm(summary, moveFirst ? "Extreme Range" : "Short Range") == (moveFirst ? -4 : 1),
                "Resolution range modifier follows ordered movement"
            );
            Check(
                Dm(summary, "Laser Sights") == (moveFirst ? 0 : 1),
                "Sight modifiers use the attack position"
            );
        }
        var e = Fresh();
        var basic = AttackModifierCalculator.Calculate(e, actor, roster, loadout)!;
        Check(
            basic.Total == 7
                && Dm(basic, "Gun Combat (Slug)") == 2
                && Dm(basic, "DEX") == 2
                && Dm(basic, "Short Range") == 1
                && Dm(basic, "Laser Sights") == 1
                && Dm(basic, "P-HUD") == 1,
            "Base attack skill, DEX, short range and attachments total"
        );
        e.ChooseAction(1, "Aim", roster);
        Check(
            Dm(AttackModifierCalculator.Calculate(e, actor, roster, loadout)!, "Aim") == 1,
            "Shot Aim bonus applied"
        );
        e.SetTargetReaction(1, "Dive for Cover", roster);
        var covering = AttackModifierCalculator.Calculate(e, actor, roster, loadout)!;
        Check(
            Dm(covering, "Target prone") == 0 && Dm(covering, "Target Hiding") == -2,
            "Dive for Cover applies Hiding with X-Ray bonus"
        );
        e.SetTargetReaction(1, "Dive for Prone", roster);
        Check(
            Dm(AttackModifierCalculator.Calculate(e, actor, roster, loadout)!, "Target prone") == -1
                && Dm(
                    AttackModifierCalculator.Calculate(e, actor, roster, loadout)!,
                    "Target behind cover"
                ) == 0,
            "Dive for Prone does not imply cover"
        );
        e.SetTargetReaction(1, "Dodge", roster);
        Check(
            Dm(AttackModifierCalculator.Calculate(e, actor, roster, loadout)!, "Target Dodge")
                == -3,
            "Dodge uses better Athletics rank"
        );
        Check(
            Dm(
                AttackModifierCalculator.Calculate(
                    e,
                    actor,
                    [actor, target with { Upp = "7F7777", Skills = [] }],
                    loadout
                )!,
                "Target Dodge"
            ) == -3,
            "Dodge uses better DEX DM"
        );
        Check(
            Dm(
                AttackModifierCalculator.Calculate(
                    e,
                    actor,
                    [actor, target with { Upp = "777777", Skills = [] }],
                    loadout
                )!,
                "Target Dodge"
            ) == -1,
            "Dodge has minimum minus one"
        );
        e.SetTargetReaction(1, "None", roster);
        var close = AttackModifierCalculator.Calculate(
            e,
            actor,
            [actor, target with { Column = 33 }],
            loadout
        )!;
        var distant = AttackModifierCalculator.Calculate(
            e,
            actor,
            [actor, target with { Column = 34 }],
            loadout
        )!;
        Check(
            Dm(close, "Laser Sights") == 1
                && Dm(close, "Holographic Sights") == 0
                && Dm(distant, "Laser Sights") == 0
                && Dm(distant, "Holographic Sights") == 1,
            "Sight bonuses switch at fifty metres"
        );
        var farRoster = new[] { actor, target with { Column = 80 } };
        Check(
            Dm(AttackModifierCalculator.Calculate(e, actor, farRoster, loadout)!, "Extreme Range")
                == -4,
            "Beyond one hundred metres without scope is extreme"
        );
        weapon.Scope = true;
        Check(
            Dm(AttackModifierCalculator.Calculate(e, actor, farRoster, loadout)!, "Long Range")
                == -2,
            "Scope plus Aim permits normal long range"
        );
        Check(
            !AttackModifierCalculator
                .Calculate(e, actor, [actor, target with { Column = 300 }], loadout)!
                .CanResolve,
            "Out of range cannot resolve"
        );
        Check(
            Dm(
                AttackModifierCalculator.Calculate(
                    e,
                    actor with
                    {
                        Skills = [new(0, 1, "Gun Combat (Energy)", 4)],
                    },
                    roster,
                    loadout
                )!,
                "Gun Combat (Slug)"
            ) == 0,
            "Attack uses basic related specialization rank"
        );
        Check(
            Dm(
                AttackModifierCalculator.Calculate(
                    e,
                    actor with
                    {
                        Skills = [new(0, 1, "Jack-of-All-Trades", 2)],
                    },
                    roster,
                    loadout
                )!,
                "Gun Combat (Slug)"
            ) == -1,
            "Attack includes unskilled Jack penalty"
        );
        e.SetHp(1, 18, roster);
        Check(
            Dm(AttackModifierCalculator.Calculate(e, actor, roster, loadout)!, "DEX") == 1,
            "Attack uses current wounded DEX"
        );
        var snapshot = e.Snapshot();
        var states = new Dictionary<int, CombatantActionState>(snapshot.Actions!);
        states[1] = e.ActionFor(1) with { TargetTagged = true, ReactionCount = 2 };
        states[2] = e.ActionFor(2) with { BehindCover = true, Stance = "Prone" };
        e.Restore(snapshot with { Actions = states }, roster);
        var tagged = AttackModifierCalculator.Calculate(e, actor, roster, loadout)!;
        Check(
            Dm(tagged, "Smart Tracker — tagged target") == 2
                && Dm(tagged, "Reactions Made") == -2
                && Dm(tagged, "Target prone") == -1
                && Dm(tagged, "Target behind cover") == -2,
            "Tagged target, own reactions and actual target statuses apply"
        );
        e.ResolveActions(1, roster, true, tagged);
        Check(
            e.ActionFor(1).ResolvedAttackModifiers!.Total == tagged.Total
                && !e.ActionFor(1).TargetHasBeenHit,
            "Resolution saves total without manufacturing a hit"
        );
        var resumed = new Encounter();
        resumed.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(JsonSerializer.Serialize(e.Snapshot()))!,
            roster
        );
        resumed.SetHp(1, 26, roster);
        Check(
            AttackModifierCalculator.Calculate(resumed, actor, roster, loadout)!.Total
                == tagged.Total,
            "Resolved modifier breakdown persists and stays fixed after healing"
        );
        Check(
            AttackModifierCalculator.Calculate(Fresh(), actor, roster, null)!.CanResolve == false,
            "Missing ranged weapon is explicit"
        );
        var conditions = Fresh();
        conditions.SetElevated(1, true, roster);
        var conditionsSnapshot = conditions.Snapshot();
        var conditionStates = new Dictionary<int, CombatantActionState>(
            conditionsSnapshot.Actions!
        );
        conditionStates[2] = conditions.ActionFor(2) with { Hiding = true };
        conditions.Restore(conditionsSnapshot with { Actions = conditionStates }, roster);
        var conditionModifiers = AttackModifierCalculator.Calculate(
            conditions,
            actor,
            roster,
            loadout
        )!;
        Check(
            Dm(conditionModifiers, "Elevated") == 1
                && Dm(conditionModifiers, "Target Hiding") == -2,
            "Elevated attacker and Hiding target modifiers"
        );
        var aiming = new Encounter();
        aiming.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
        aiming.ChooseAction(1, "Aim", roster, 3);
        aiming.ChooseAction(1, "Aim", roster, 3);
        aiming.SetTarget(1, 2, roster);
        var aimPreview = AttackModifierCalculator.Calculate(aiming, actor, farRoster, loadout)!;
        Check(
            Dm(aimPreview, "Aim") == 2 && Dm(aimPreview, "Long Range") == -2,
            "Aim-only preview assumes a following ranged attack, including scope benefit"
        );
        var afterShot = Fresh();
        afterShot.ChooseAction(1, "Aim", roster, 3);
        Check(
            Dm(AttackModifierCalculator.Calculate(afterShot, actor, roster, loadout)!, "Aim") == 0,
            "Aim after a selected shot never boosts that shot"
        );
        var incompleteAim = new Encounter();
        incompleteAim.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
        incompleteAim.ChooseAction(1, "Aim", roster);
        var incompletePreview = AttackModifierCalculator.Calculate(
            incompleteAim,
            actor,
            roster,
            loadout
        )!;
        Check(!incompletePreview.CanResolve, "Aim preview reports missing target");
        incompleteAim.ResolveActions(1, roster, attackModifiers: incompletePreview);
        Check(
            incompleteAim.ActionFor(1).ActionsResolved,
            "Incomplete attack preview does not prevent resolving Aim itself"
        );
        var carriedAim = Fresh();
        var carriedSnapshot = carriedAim.Snapshot();
        var carriedStates = new Dictionary<int, CombatantActionState>(carriedSnapshot.Actions!);
        carriedStates[1] = carriedAim.ActionFor(1) with { CarriedAimBonus = 3 };
        carriedAim.Restore(carriedSnapshot with { Actions = carriedStates }, roster);
        carriedAim.ChooseAction(1, "Aim", roster);
        var carriedModifiers = AttackModifierCalculator.Calculate(
            carriedAim,
            actor,
            roster,
            loadout
        )!;
        Check(
            Dm(carriedModifiers, "Aim (+3 carried over)") == 4,
            "Aim label distinguishes carried +3 from total +4"
        );
        carriedAim.ChooseAction(1, "Ranged Attack", roster, 2);
        carriedAim.ChooseAction(1, "Move", roster);
        carriedAim.ChooseAction(1, "Aim", roster, 3);
        var interruptedModifiers = AttackModifierCalculator.Calculate(
            carriedAim,
            actor,
            roster,
            loadout
        )!;
        Check(
            Dm(interruptedModifiers, "Aim") == 1
                && !interruptedModifiers.Rows.Any(row => row.Name.Contains("carried over")),
            "Interrupted Aim does not claim a carried bonus"
        );
        Console.WriteLine(
            "Attack modifier skill/DEX/Aim, ranges, reaction previews, Dodge, sights, tagging, P-HUD, X-Ray, totals and persistence passed."
        );
    }
}
