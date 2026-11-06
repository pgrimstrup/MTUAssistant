using System.Text.Json;
using MTU.Core;

public static class AutoAttackChecks
{
    public static void Run()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        var actor = new SceneCombatant(
            1,
            1,
            "Hero",
            false,
            false,
            true,
            0,
            0,
            "777777",
            Skills: [new(0, 1, "Gun Combat (Slug)", 2)]
        );
        var one = new SceneCombatant(2, 2, "One", true, false, true, 2, 0, "777777");
        var two = new SceneCombatant(3, 3, "Two", true, false, true, 3, 0, "777777");
        var three = new SceneCombatant(4, 4, "Three", true, false, true, 4, 0, "777777");
        var roster = new[] { actor, one, two, three };
        var loadout = new CombatLoadout([
            new()
            {
                Equipped = true,
                Equipment = new()
                {
                    Kind = EquipmentKind.Weapon,
                    Name = "Rifle",
                    Damage = "3D",
                    Range = 1000,
                    RequiredSkill = "Gun Combat (Slug)",
                    Traits = "Auto 3, Scope",
                },
            },
        ]);
        Encounter Start()
        {
            var result = new Encounter();
            result.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
            result.ChooseAction(1, "Aim", roster, 1);
            result.ChooseAction(1, "Ranged Attack", roster, 2);
            result.SetTarget(1, 2, roster);
            return result;
        }
        Check(
            loadout.AutoScore == 3
                && AutoFire.Score("AP 5, Auto (4)") == 4
                && AutoFire.Score("Scope") == 0,
            "Auto score parsing"
        );
        var single = Start();
        Check(
            single.ActionFor(1).RangedAimBonus == 1 && loadout.RangeFor(single.ActionFor(1))!.Scope,
            "Single preserves Aim and Scope"
        );
        var baseline = single.BeginAttack(1, roster, loadout, null, 0);
        baseline.SetAttackRoll(10);
        baseline.SetDamageRoll(7);
        Check(baseline.BurstDamageBonus == 0, "Single has no Burst damage");
        var burst = Start();
        burst.SetAutoAttack(1, "Burst", 3, roster);
        Check(
            !burst.ActionFor(1).IsSelected("Aim")
                && burst.ActionFor(1).RangedAimBonus == 0
                && !burst.CanChooseAction(1, "Aim")
                && !loadout.RangeFor(burst.ActionFor(1))!.Scope,
            "Automatic fire removes and blocks Aim and Scope"
        );
        var burstAttack = burst.BeginAttack(1, roster, loadout, null, 0);
        burstAttack.SetAttackRoll(10);
        burstAttack.SetDamageRoll(7);
        Check(
            burstAttack.Outcome!.TotalDamage == 7 + burstAttack.Outcome.Effect + 3
                && burstAttack.Outcome.DamageSummary!.Contains("Burst 3"),
            "Burst adds Auto score once to damage"
        );
        burstAttack.AutoRoll(_ => 6);
        Check(
            burstAttack.Outcome!.TotalDamage == 18 + burstAttack.Outcome.Effect + 3,
            "Auto rolling retains Burst damage"
        );
        var full = Start();
        full.SetAutoAttack(1, "Full Auto", 3, roster);
        full.SetAttackTarget(1, 1, 3, roster);
        full.SetAttackTarget(1, 2, 4, roster);
        full.SetAttackReaction(1, 1, "Dive for Prone", roster);
        Check(
            full.ActionFor(1).TargetIds.SequenceEqual(new[] { 2, 3, 4 })
                && full.MovementRanges(roster).Any(r => r.Id == 3),
            "Full Auto has three targets and independent reaction movement"
        );
        var drafts = Enumerable
            .Range(0, 3)
            .Select(i => full.BeginAttack(1, roster, loadout, null, 0, targetIndex: i))
            .ToArray();
        Check(
            drafts[1].Modifiers.Total == drafts[0].Modifiers.Total - 1
                && drafts.All(d => d.BurstDamageBonus == 0),
            "Full Auto independently applies each reaction with Single damage"
        );
        foreach (var draft in drafts)
        {
            draft.SetAttackRoll(10);
            draft.SetDamageRoll(7);
        }
        full.PreviewAttack(1, drafts[1].Outcome, 1);
        full.PreviewAttack(1, null);
        Check(
            full.Hp(two) == 21
                && !full.ActionFor(1).ActionsResolved
                && full.ActionFor(1).AttackOutcome is null,
            "Cancelling volley preview does not commit damage"
        );
        full.ApplyAttacks(drafts, roster);
        Check(
            full.ActionFor(1).AutoAttackResults?.Count == 3
                && full.ActionFor(3).Stance == "Prone"
                && full.Hp(one) < 21
                && full.Hp(two) < 21
                && full.Hp(three) < 21,
            "Confirming volley applies every target and reaction"
        );
        var restored = new Encounter();
        restored.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(full.Snapshot())
            )!,
            roster
        );
        Check(
            restored.ActionFor(1).AutoAttackResults?.Count == 3 && restored.Hp(two) == full.Hp(two),
            "Full Auto results and health persist"
        );
        var summary = CombatActionSummary.Create(full, actor, roster, loadout, player: true)!;
        Check(
            summary.Rows.Count(r => r.Label.StartsWith("Attack Result")) == 3
                && summary.TargetAttackModifiers?.Count == 3
                && summary
                    .Rows.Where(r => r.Label.StartsWith("Damage Result"))
                    .All(r => !r.Value.Contains("injury", StringComparison.OrdinalIgnoreCase)),
            "Cast displays per-target results and preserves NPC injury privacy"
        );
        var hidden = CombatActionSummary.Create(
            full,
            actor,
            [actor, one, two with { IsVisible = false }, three],
            loadout,
            player: true
        )!;
        Check(
            hidden.TargetAttackModifiers?.Count == 2
                && hidden.Rows.All(r => !r.Label.Contains("Two")),
            "Hidden targets remain private"
        );
        var rangeRoster = new[]
        {
            actor,
            one with
            {
                Column = 4,
            },
            two with
            {
                Column = 2,
            },
            three with
            {
                Column = 8,
            },
        };
        var rangeLoadout = new CombatLoadout([
            new()
            {
                Equipped = true,
                Equipment = new()
                {
                    Kind = EquipmentKind.Weapon,
                    Name = "SMG",
                    Damage = "3D",
                    Range = 20,
                    RequiredSkill = "Gun Combat (Slug)",
                    Traits = "Auto 3",
                },
            },
        ]);
        var ranged = new Encounter();
        ranged.Begin(rangeRoster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
        ranged.ChooseAction(1, "Ranged Attack", rangeRoster, 2);
        ranged.SetAttackTarget(1, 0, 2, rangeRoster);
        ranged.SetAutoAttack(1, "Full Auto", 3, rangeRoster);
        ranged.SetAttackTarget(1, 1, 3, rangeRoster);
        ranged.SetAttackTarget(1, 2, 4, rangeRoster);
        var rangeRows = Enumerable
            .Range(0, 3)
            .Select(i =>
                AttackModifierCalculator.Calculate(ranged, actor, rangeRoster, rangeLoadout, i)!
            )
            .ToArray();
        Check(
            rangeRows
                .Select(r => r.Rows.Single(m => m.Name == "Range").Dm)
                .SequenceEqual(new[] { 0, 1, 0 }),
            "Full Auto retains each target range modifier"
        );
        Check(
            Encounter.WithinAutoTargetRange(rangeRoster[1], rangeRoster[3])
                && !Encounter.WithinAutoTargetRange(
                    rangeRoster[1],
                    rangeRoster[3] with
                    {
                        Row = 1,
                    }
                ),
            "Secondary target limit includes exactly 6m but excludes diagonal distances over 6m"
        );
        ranged.SetAttackTarget(1, 0, 3, rangeRoster);
        Check(
            ranged.ActionFor(1).AttackTargets[2].TargetId is null,
            "Changing first target clears secondaries outside 6m"
        );
        var rejected = false;
        try
        {
            ranged.SetAttackTarget(1, 2, 4, rangeRoster);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Check(rejected, "Secondary targets beyond 6m cannot be selected");
        Check(
            summary.Rows.Any(r => r.Label == "Attack Result 1")
                && summary.Rows.All(r => !r.Label.Contains(" — ")),
            "Full Auto result labels use attack numbers"
        );
        var hidingEncounter = Start();
        hidingEncounter.SetTargetReaction(1, "Dive for Cover", roster);
        hidingEncounter.SetTargetReactionCover(1, new CombatCover(1, "Vegetation", 2, 1), roster);
        hidingEncounter.SetTargetReactionStance(1, "Prone", roster);
        var hidingModifiers = AttackModifierCalculator.Calculate(
            hidingEncounter,
            actor,
            roster,
            loadout
        )!;
        Check(
            hidingModifiers.Rows.Any(r => r.Name == "Target Hiding" && r.Dm == -2)
                && hidingModifiers.Rows.Any(r => r.Name == "Target prone" && r.Dm == -1),
            "Dive for Cover previews Hiding and reaction stance modifiers"
        );
        Check(
            hidingEncounter.MovementRanges(roster).Any(r => r.Id == 2 && r.Diameter == 4.5),
            "Dive for Cover displays the same 1.5m movement circle as its reaction"
        );
        var hideAttack = hidingEncounter.BeginAttack(1, roster, loadout, null, 0);
        Check(
            hideAttack.OverallProtection == 2,
            "Dive for Cover applies cover protection immediately during attack resolution"
        );
        hideAttack.SetAttackRoll(3);
        hidingEncounter.ApplyAttack(hideAttack, roster);
        Check(
            hidingEncounter.ActionFor(2).Hiding
                && hidingEncounter.ActionFor(2).BehindCover
                && hidingEncounter.ActionFor(2).Stance == "Prone",
            "Dive for Cover commits Hiding instead of Behind Cover and the chosen stance"
        );
        var extraHide = Start();
        extraHide.SetAutoAttack(1, "Full Auto", 3, roster);
        extraHide.SetAttackTarget(1, 1, 3, roster);
        extraHide.SetAttackReaction(1, 1, "Dive for Cover", roster);
        extraHide.SetAttackReactionStance(1, 1, "Crouching", roster);
        Check(
            extraHide.AttackStateFor(1, 1).IsDive
                && extraHide.AttackStateFor(1, 1).TargetReactionStance == "Crouching",
            "Secondary Dive for Cover supports stance selection"
        );
        Check(
            extraHide.BeginAttack(1, roster, loadout, null, 0, targetIndex: 1).OverallProtection
                == 0,
            "Secondary dive uses the target's existing cover protection"
        );
        foreach (var status in new[] { "Hiding", "Unconscious", "Death" })
        {
            var cleanup = Start();
            cleanup.SetAutoAttack(1, "Full Auto", 3, roster);
            cleanup.SetAttackTarget(1, 1, 3, roster);
            cleanup.ChooseAction(2, "Ranged Attack", roster, 2);
            cleanup.SetTarget(2, 1, roster);
            switch (status)
            {
                case "Reset":
                    cleanup.ResetActions(1, roster);
                    break;
                case "Hiding":
                    cleanup.SetCoverStatus(1, false, true, roster);
                    break;
                case "Unconscious":
                    cleanup.SetUnconscious(1, true, roster);
                    break;
                case "Death":
                    cleanup.SetHp(1, 0, roster);
                    break;
            }
            Check(
                cleanup.ActionFor(1).TargetIds.Count == 0 && cleanup.ActionFor(2).TargetId == 1,
                status + " clears outgoing targets while preserving incoming targets"
            );
        }
        var retainedTargets = Start();
        retainedTargets.SetAutoAttack(1, "Full Auto", 3, roster);
        retainedTargets.SetAttackTarget(1, 1, 3, roster);
        retainedTargets.SetAttackReaction(1, 1, "Dive for Cover", roster);
        retainedTargets.ResetActions(1, roster);
        retainedTargets.RefreshAutoWeapon(1, loadout.AutoScore, roster);
        Check(
            retainedTargets.ActionFor(1).TargetIds.SequenceEqual(new[] { 2, 3 })
                && retainedTargets.ActionFor(1).AttackTargets.All(t => t.Reaction == "None"),
            "Reset preserves all targets and clears reactions"
        );
        retainedTargets.ChooseAction(1, "Ranged Attack", roster, 2);
        Check(
            retainedTargets.ActionFor(1).FullAuto
                && retainedTargets.ActionFor(1).TargetIds.SequenceEqual(new[] { 2, 3 }),
            "Reselecting Ranged Attack restores the retained Full Auto targets"
        );
        var exit = Start();
        var exitSnapshot = exit.Snapshot();
        foreach (var combatant in roster)
            exitSnapshot.Actions![combatant.Id] = new CombatantActionState(
                TargetId: combatant.IsNpc ? 1 : 2,
                Stance: "Prone",
                BehindCover: true,
                Hiding: true,
                Unconscious: combatant.Id == 3
            );
        exit.Restore(exitSnapshot, roster);
        exit.SetHp(4, 0, roster);
        exit.Exit(roster);
        Check(
            roster.All(e => exit.ActionFor(e.Id).TargetIds.Count == 0),
            "Exit clears every outgoing target"
        );
        Check(
            exit.ActionFor(1).Stance == "Standing"
                && !exit.ActionFor(1).Hiding
                && !exit.ActionFor(1).BehindCover
                && exit.ActionFor(2).Stance == "Standing",
            "Exit resets conscious living combatants"
        );
        Check(
            new[] { 3, 4 }.All(id =>
                exit.ActionFor(id).Stance == "Prone"
                && exit.ActionFor(id).Hiding
                && exit.ActionFor(id).BehindCover
            ),
            "Exit preserves unconscious and dead combatant stance and cover statuses"
        );
        foreach (var sameSideIsNpc in new[] { false, true })
        {
            var alliedActor = actor with { IsNpc = sameSideIsNpc };
            var alliedOne = one with { IsNpc = sameSideIsNpc };
            var alliedTwo = two with { IsNpc = sameSideIsNpc };
            var alliedRoster = new[] { alliedActor, alliedOne, alliedTwo };
            var allied = new Encounter();
            allied.Begin(
                alliedRoster,
                true,
                true,
                0,
                new Dictionary<int, int>
                {
                    [1] = 10,
                    [2] = 9,
                    [3] = 8,
                },
                () => 7
            );
            allied.ChooseAction(1, "Ranged Attack", alliedRoster, 2);
            allied.SetTarget(1, 2, alliedRoster);
            allied.SetTargetReaction(1, "Dodge", alliedRoster);
            allied.SetAutoAttack(1, "Full Auto", 2, alliedRoster);
            allied.SetAttackTarget(1, 1, 3, alliedRoster);
            allied.SetAttackReaction(1, 1, "Dodge", alliedRoster);
            var gun = new CombatLoadout([
                new CharacterEquipmentDetails
                {
                    Equipped = true,
                    Equipment = new EquipmentItem
                    {
                        Kind = EquipmentKind.Weapon,
                        Name = "Rifle",
                        RequiredSkill = "Gun Combat (Slug)",
                        Range = 100,
                        Damage = "3D",
                        Traits = "Auto 2",
                    },
                },
            ]);
            var sameSideHit = allied.BeginAttack(1, alliedRoster, gun, new CombatLoadout([]), 0);
            sameSideHit.SetAttackRoll(10);
            sameSideHit.SetDamageRoll(10);
            Check(sameSideHit.TargetId == 2, "Same-side combatants can be targeted and attacked");
            Check(
                allied
                    .BeginAttack(1, alliedRoster, gun, new CombatLoadout([]), 0, targetIndex: 1)
                    .TargetId == 3,
                "Full Auto can attack another combatant on the same side"
            );
            var selfRejected = false;
            try
            {
                allied.SetTarget(1, 1, alliedRoster);
            }
            catch (ArgumentException)
            {
                selfRejected = true;
            }
            Check(selfRejected, "Primary self-targeting remains forbidden");
            selfRejected = false;
            try
            {
                allied.SetAttackTarget(1, 1, 1, alliedRoster);
            }
            catch (ArgumentException)
            {
                selfRejected = true;
            }
            Check(selfRejected, "Full Auto self-targeting remains forbidden");
            var secondaryHit = allied.BeginAttack(
                1,
                alliedRoster,
                gun,
                new CombatLoadout([]),
                0,
                targetIndex: 1
            );
            secondaryHit.SetAttackRoll(10);
            secondaryHit.SetDamageRoll(10);
            allied.ApplyAttacks([sameSideHit, secondaryHit], alliedRoster);
            Check(
                allied.ActionFor(1).ActionsResolved
                    && allied.Hp(alliedOne) < Encounter.MaximumHp(alliedOne),
                "Same-side attacks resolve and apply injury"
            );
        }
        Console.WriteLine(
            "Auto modes, Aim/Scope suppression, Burst damage, per-target Full Auto resolution, persistence and Cast privacy passed."
        );
    }
}
