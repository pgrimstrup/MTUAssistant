using System.Text.Json;
using MTU.Core;

public static class CombatActionChecks
{
    public static void Run()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        var rifle = new CharacterEquipmentDetails
        {
            Equipped = true,
            Equipment = new EquipmentItem
            {
                Kind = EquipmentKind.Weapon,
                Name = "Rifle",
                Range = 100,
                Damage = "3D",
                Traits = "Scope",
            },
        };
        var loadout = new CombatLoadout([
            rifle,
            new()
            {
                Equipped = true,
                Equipment = new()
                {
                    Kind = EquipmentKind.Armour,
                    Name = "Suit",
                    Protection = 12,
                },
            },
            new()
            {
                Equipped = true,
                Equipment = new()
                {
                    Kind = EquipmentKind.Armour,
                    Name = "Helmet",
                    Protection = 2,
                },
            },
            new()
            {
                Equipment = new() { Kind = EquipmentKind.Weapon, Name = "Unequipped" },
            },
        ]);
        Check(
            loadout.Armour == "Suit (DR +12), Helmet (DR +2)"
                && loadout.Weapons == "Rifle (100m, 3D)",
            "Equipped gear summary"
        );
        var profile = new CombatWeaponRange(20, true, 1);
        foreach (
            var (distance, band) in new[]
            {
                (4.9, "Short Range"),
                (5d, "Medium Range"),
                (20d, "Long Range"),
                (40d, "Extreme Range"),
                (80d, "Out of Range"),
            }
        )
            Check(profile.Band(distance) == band, "Strict range boundary " + distance);
        Check(
            new CombatWeaponRange(1000, false, 6).Band(101) == "Extreme Range",
            "Aim needs a scope beyond 100m"
        );
        Check(
            new CombatWeaponRange(1000, true, 0).Band(101) == "Extreme Range",
            "Scope needs Aim beyond 100m"
        );
        Check(
            new CombatWeaponRange(1000, true, 1).Band(101) == "Short Range",
            "Scoped Aim restores normal bands"
        );
        Check(
            new CombatWeaponRange(1000, false, 0).Band(100) == "Short Range",
            "100m itself uses normal bands"
        );
        Check(
            loadout
                .RangeFor(
                    new CombatantActionState(
                        CarriedAimBonus: 3,
                        MajorAction: "Ranged Attack",
                        AfterMajorActions: ["Aim"]
                    )
                )
                ?.AimBonus == 3,
            "Scope uses Aim at shot, before it is consumed"
        );
        rifle.Equipment.Traits = "";
        rifle.Scope = true;
        Check(
            loadout.RangeFor(new CombatantActionState(CarriedAimBonus: 1))?.Scope == true,
            "Scope attachment supported"
        );
        var hero = new SceneCombatant(
            1,
            1,
            "Hero",
            false,
            false,
            true,
            2,
            3,
            "777777",
            null,
            "Player"
        );
        var enemy = new SceneCombatant(
            2,
            2,
            "Guard",
            true,
            false,
            false,
            7,
            8,
            "777777",
            null,
            "",
            "Mercenary"
        );
        var roster = new[] { hero, enemy };
        Encounter Fresh()
        {
            var e = new Encounter();
            e.Begin(roster, true, true, 0, new Dictionary<int, int> { { 1, 20 } }, () => 7);
            return e;
        }
        var aimShoot = Fresh();
        aimShoot.SetTarget(1, 2, roster);
        aimShoot.ChooseAction(1, "Aim", roster, 1);
        aimShoot.ChooseAction(1, "Ranged Attack", roster, 2);
        Check(
            aimShoot.ActionFor(1).SequenceSummary == "Aim → Ranged Attack"
                && aimShoot.ActionFor(1).RangedAimBonus == 1,
            "Aim before shooting contributes to the shot"
        );
        var shootAim = Fresh();
        shootAim.ChooseAction(1, "Aim", roster, 3);
        shootAim.ChooseAction(1, "Ranged Attack", roster, 2);
        Check(
            shootAim.ActionFor(1).SequenceSummary == "Ranged Attack → Aim"
                && shootAim.ActionFor(1).RangedAimBonus == 0
                && shootAim.ActionFor(1).AimBonus == 1,
            "Shoot-Aim has no new Aim bonus on this shot, regardless of click order"
        );
        Check(aimShoot.ActionFor(1).AimBonus == 0, "Ranged Attack consumes its Aim bonus");
        var aimAimMove = Fresh();
        aimAimMove.ChooseAction(1, "Aim", roster, 1);
        aimAimMove.ChooseAction(1, "Aim", roster, 1);
        aimAimMove.ChooseAction(1, "Move", roster, 3);
        Check(
            aimAimMove.ActionFor(1).SequenceSummary == "Aim → Aim → Move"
                && aimAimMove.ActionFor(1).AimBonus == 0,
            "Aim-Aim-Move clears the Aim bonus"
        );
        var carriedShot = Fresh();
        for (var count = 0; count < 3; count++)
            carriedShot.ChooseAction(1, "Aim", roster, 1);
        carriedShot.ResolveActions(1, roster);
        carriedShot.Next(roster);
        carriedShot.Next(roster);
        carriedShot.ChooseAction(1, "Ranged Attack", roster, 2);
        carriedShot.ChooseAction(1, "Aim", roster, 3);
        Check(
            carriedShot.ActionFor(1).RangedAimBonus == 3 && carriedShot.ActionFor(1).AimBonus == 1,
            "Shoot uses carried +3, resets it, then Aim creates +1"
        );
        var resumedShot = new Encounter();
        resumedShot.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(carriedShot.Snapshot())
            )!,
            roster
        );
        Check(
            resumedShot.ActionFor(1).RangedAimBonus == 3 && resumedShot.ActionFor(1).AimBonus == 1,
            "Aim consumption survives reload"
        );
        resumedShot.SetTarget(1, 2, roster);
        resumedShot.ResolveActions(1, roster);
        resumedShot.Next(roster);
        resumedShot.Next(roster);
        Check(
            resumedShot.ActionFor(1).AimBonus == 1,
            "Only post-shot Aim carries into the next turn"
        );
        var reloadAim = Fresh();
        reloadAim.ChooseAction(1, "Reload (fast)", roster, 1);
        reloadAim.ChooseAction(1, "Aim", roster, 3);
        reloadAim.ChooseAction(1, "Aim", roster, 3);
        Check(
            reloadAim.ActionFor(1).MinorCount == 3
                && reloadAim.ActionFor(1).SequenceSummary == "Reload (fast) → Aim → Aim"
                && reloadAim.ActionFor(1).AimBonus == 2,
            "Reload-Aim-Aim preserves explicit order and a shared minor total"
        );
        var moveReloadAim = Fresh();
        moveReloadAim.ChooseAction(1, "Move", roster, 1);
        moveReloadAim.ChooseAction(1, "Reload (fast)", roster, 1);
        moveReloadAim.ChooseAction(1, "Aim", roster, 3);
        Check(
            moveReloadAim.ActionFor(1).SequenceSummary == "Move → Reload (fast) → Aim"
                && moveReloadAim.ActionFor(1).AimBonus == 1,
            "Move-Reload-Aim is explicit"
        );
        var e = Fresh();
        e.ChooseAction(1, "Aim", roster, 1);
        e.ChooseAction(1, "Reload (fast)", roster, 3);
        Check(
            e.ActionFor(1).AimBonus == 0 && e.ActionFor(1).AimSelections == 1,
            "A later non-ranged action clears the bonus without erasing its earlier Aim selection"
        );
        e = Fresh();
        e.ChooseAction(1, "Reload (fast)", roster, 1);
        e.ChooseAction(1, "Reload (fast)", roster, 3);
        Check(
            e.ActionFor(1).MinorCount == 2
                && e.ActionFor(1).ColumnActions(1).Count == 1
                && e.ActionFor(1).ColumnActions(3).Count == 1,
            "Duplicated minor columns have independent selections"
        );
        Check(
            CombatActionChoices.Major.All(a => !e.CanChooseAction(1, a)),
            "Two minors leave no available major action"
        );
        e.ChooseAction(1, "Reload (fast)", roster, 3);
        e.ChooseAction(1, "Leadership", roster, 2);
        e.ChooseAction(1, "Skill Check", roster, 2);
        Check(e.ActionFor(1).MajorAction == "Skill Check", "Major selections remain exclusive");
        e.ChooseAction(1, "Aim", roster, 3);
        Check(e.ActionFor(1).MajorAction is null, "Adding the second minor clears major selection");
        e = Fresh();
        for (var i = 1; i <= 3; i++)
        {
            e.ChooseAction(1, "Aim", roster, 3);
            Check(
                e.ActionFor(1).AimSelections == i && e.ActionFor(1).AimBonus == i,
                "Aim cycles within its own column"
            );
        }
        e.ResolveActions(1, roster);
        e.Next(roster);
        e.Next(roster);
        Check(
            e.ActionFor(1).MinorCount == 0 && e.ActionFor(1).AimBonus == 3,
            "Aim carries while both minor columns reset"
        );
        for (var i = 1; i <= 3; i++)
            e.ChooseAction(1, "Aim", roster, 1);
        Check(e.ActionFor(1).AimBonus == 6, "Aim remains capped at six");
        e.ChooseAction(1, "Aim", roster, 1);
        Check(e.ActionFor(1).AimBonus == 3, "Cycling off retains carried Aim");
        e = Fresh();
        foreach (var count in new[] { 1, 2, 3 })
        {
            e.ChooseAction(1, "Move", roster, 3);
            Check(
                e.ActionFor(1).MoveSelections == count
                    && e.MovementRanges(roster).Single().Diameter == 1.5 + 12 * count,
                "Move cycles in either minor column"
            );
        }
        e.ChooseAction(1, "Move", roster, 3);
        Check(e.MovementRanges(roster).Count == 0, "Fourth Move click clears the circle");
        var restored = new Encounter();
        restored.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(moveReloadAim.Snapshot())
            )!,
            roster
        );
        Check(
            restored.ActionFor(1).SequenceSummary == moveReloadAim.ActionFor(1).SequenceSummary
                && restored.ActionFor(1).AimBonus == 1,
            "Ordered selections survive reload"
        );
        restored.ResolveActions(1, roster);
        restored.Next(roster);
        Check(
            restored.MovementRanges(roster).Count == 0
                && restored.ActionFor(1).MinorCount == 0
                && restored.ActionFor(1).AimBonus == 1,
            "Turn end clears ordered selections and ranges while carrying final Aim"
        );
        var generic = hero with { Tactics = 2 };
        Check(
            Encounter.TravellerTacticsLevel(generic) == 2
                && Encounter.TravellerTacticsLevel(hero) is null,
            "Generic Tactics qualifies and missing skill remains untrained"
        );
        Check(
            Encounter.RollTravellers(new[] { generic, enemy }, true, () => 7).Tactics == 1,
            "Auto-initiative rolls generic Tactics as well as Military Tactics"
        );
        Check(
            Encounter.RollTravellers(new[] { generic }, false, () => 7).Tactics == 0,
            "Unaware Travellers do not use Tactics"
        );
        e = Fresh();
        e.ChooseAction(1, "Draw/Holster", roster, 1);
        e.ChooseAction(1, "Other", roster, 1);
        e.ChooseAction(1, "Move", roster, 3);
        e.ChooseAction(1, "Move", roster, 3);
        Check(
            e.ActionFor(1).MinorCount == 4
                && CombatActionChoices.Minor.All(a => e.CanChooseAction(1, a)),
            "GM can manage the overall minor allowance without blocked cycles or errors"
        );
        var originCheck = Fresh();
        var movedRoster = new[] { hero with { Column = 8, Row = 9 }, enemy };
        originCheck.ChooseAction(1, "Move", movedRoster, 1);
        Check(
            originCheck.MovementRanges(movedRoster).Single() is { Column: 2, Row: 3 },
            "Selecting Move after moving still uses the turn-start cell"
        );
        originCheck.ChooseAction(1, "Move", movedRoster, 1);
        originCheck.ChooseAction(1, "Move", movedRoster, 1);
        originCheck.ChooseAction(1, "Move", movedRoster, 1);
        originCheck.ChooseAction(1, "Move", movedRoster, 3);
        Check(
            originCheck.MovementRanges(movedRoster).Single() is { Column: 2, Row: 3 },
            "Cycling Move off and on never shifts the turn origin"
        );
        var restoredOrigin = new Encounter();
        restoredOrigin.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(originCheck.Snapshot())
            )!,
            movedRoster
        );
        Check(
            restoredOrigin.MovementRanges(movedRoster).Single() is { Column: 2, Row: 3 },
            "Turn origin persists across reload after the token has moved"
        );
        restoredOrigin.Next(movedRoster);
        restoredOrigin.Next(movedRoster);
        restoredOrigin.ChooseAction(1, "Move", movedRoster, 1);
        Check(
            restoredOrigin.MovementRanges(movedRoster).Single() is { Column: 8, Row: 9 },
            "Next turn captures the combatant's new starting cell"
        );
        var sameSelected = Fresh();
        sameSelected.SelectCombatant(1, movedRoster);
        sameSelected.ChooseAction(1, "Move", movedRoster);
        Check(
            sameSelected.MovementRanges(movedRoster).Single().Column == 2,
            "Re-selecting the current actor does not reset their origin"
        );
        var unplacedStart = new Encounter();
        var unplacedRoster = new[] { hero with { Column = null, Row = null }, enemy };
        unplacedStart.Begin(
            unplacedRoster,
            true,
            true,
            0,
            new Dictionary<int, int> { { 1, 20 } },
            () => 7
        );
        Check(
            unplacedStart.AnchorMovement(1, 4, 5),
            "First placement establishes an unplaced combatant's turn origin"
        );
        unplacedStart.ChooseAction(1, "Move", movedRoster);
        Check(
            unplacedStart.MovementRanges(movedRoster).Single() is { Column: 4, Row: 5 },
            "A later move uses the first placement, not its current position"
        );
        var summaryEncounter = new Encounter();
        summaryEncounter.Begin(
            [hero, enemy],
            true,
            true,
            0,
            new Dictionary<int, int> { [hero.Id] = 20 },
            () => 7
        );
        summaryEncounter.ChooseAction(hero.Id, "Ranged Attack", [hero, enemy]);
        summaryEncounter.SetTarget(hero.Id, enemy.Id, [hero, enemy]);
        summaryEncounter.SetStance(hero.Id, "Prone", [hero, enemy]);
        var summary = CombatActionSummary.Create(summaryEncounter, hero, [hero, enemy], loadout)!;
        Check(
            summary
                .Rows.Select(r => r.Label)
                .SequenceEqual(
                    new[]
                    {
                        "Combatant",
                        "Armour",
                        "Weapon",
                        "Stance",
                        "Reactions Made",
                        "Target",
                        "Target Reaction",
                        "Sequence",
                        "Result",
                    }
                ),
            "Summary row order"
        );
        Check(
            summary.Rows.Single(r => r.Label == "Stance").Value == "Prone (-1)",
            "Prone modifier displayed"
        );
        foreach (
            var (band, dm) in new[]
            {
                ("Short Range", "+1"),
                ("Medium Range", "0"),
                ("Long Range", "-2"),
                ("Extreme Range", "-4"),
            }
        )
            Check(
                CombatActionSummary.RangeDescription(band) == $"{band} (DM {dm})",
                "Range DM " + band
            );
        var farEnemy = enemy with { Column = hero.Column + 60, Row = hero.Row };
        var shortWeapon = new CombatLoadout([
            new()
            {
                Equipped = true,
                Equipment = new()
                {
                    Kind = EquipmentKind.Weapon,
                    Name = "Pistol",
                    Range = 10,
                },
            },
        ]);
        var outSummary = CombatActionSummary.Create(
            summaryEncounter,
            hero,
            [hero, farEnemy],
            shortWeapon
        )!;
        Check(
            outSummary
                .Rows.Single(r => r.Label == "Target")
                .Value.Contains("ranged attack cannot be made")
                && outSummary.Rows.Last().Value.Contains("cannot be made"),
            "Out of range blocks ranged attack in summary"
        );
        Check(
            CombatActionSummary.Create(
                summaryEncounter,
                hero with
                {
                    IsVisible = false,
                },
                [hero, enemy],
                loadout,
                true
            )
                is null,
            "Hidden actors have no public action panel"
        );
        var publicEnemy = CombatActionSummary.Create(
            summaryEncounter,
            enemy with
            {
                IsVisible = true,
            },
            [hero, enemy],
            loadout,
            true
        )!;
        Check(
            !publicEnemy.Rows.Any(r => r.Label is "Armour" or "Weapon"),
            "Enemy equipment omitted from public payload"
        );
        var publicTraveller = CombatActionSummary.Create(
            summaryEncounter,
            hero,
            [hero, enemy with { IsVisible = false }],
            loadout,
            true
        )!;
        Check(
            publicTraveller.Rows.Any(r => r.Label == "Armour")
                && publicTraveller.Rows.Single(r => r.Label == "Target").Value == "None",
            "Traveller gear shown and hidden targets omitted"
        );
        var movement = Fresh();
        movement.ChooseAction(hero.Id, "Change Stance", roster);
        movement.ChooseAction(hero.Id, "Move", roster);
        Check(
            movement.ActionFor(hero.Id).MovementDiameter == 13.5,
            "Standing movement remains unchanged"
        );
        movement.SetStance(hero.Id, "Crouching", roster);
        Check(
            movement.ActionFor(hero.Id).MovementDiameter == 7.5,
            "Crouching halves movement allowance"
        );
        movement.ChooseAction(hero.Id, "Move", roster);
        Check(movement.ActionFor(hero.Id).MovementDiameter == 13.5, "Two crouching moves");
        movement.SetStance(hero.Id, "Prone", roster);
        Check(
            movement.ActionFor(hero.Id).MovementDiameter == 7.5,
            "Prone quarters movement allowance"
        );
        movement.ChooseAction(hero.Id, "Move", roster);
        Check(movement.ActionFor(hero.Id).MovementDiameter == 10.5, "Three prone moves");
        var restoredMovement = new Encounter();
        restoredMovement.Restore(movement.Snapshot(), roster);
        Check(
            restoredMovement.MovementRanges(roster).Single().Diameter == 10.5,
            "Stance movement survives reload"
        );
        var origin = movement.ActionFor(hero.Id).TurnOriginColumn;
        movement.SetTarget(hero.Id, enemy.Id, roster);
        movement.ResolveActions(hero.Id, roster);
        movement.ResetActions(hero.Id, roster);
        var reset = movement.ActionFor(hero.Id);
        Check(
            reset.MinorCount == 0
                && reset.MajorAction is null
                && reset.MovementDiameter is null
                && movement.MovementRanges(roster).Count == 0,
            "Reset clears all selections and range circles"
        );
        Check(
            reset.Stance == "Prone"
                && reset.TargetId == enemy.Id
                && reset.TurnOriginColumn == origin,
            "Reset retains stance, target and turn origin"
        );
        movement.ChooseAction(hero.Id, "Ranged Attack", roster);
        movement.ResolveActions(hero.Id, roster);
        movement.ResetActions(hero.Id, roster);
        Check(movement.ActionFor(hero.Id).MajorAction is null, "Reset clears selected major");
        Check(
            loadout.TargetBand(
                new CombatantActionState(BeforeMajorActions: ["Aim"]),
                hero,
                farEnemy
            )
                is null,
            "Aim alone has no range band"
        );
        var aimSummaryEncounter = Fresh();
        aimSummaryEncounter.ChooseAction(hero.Id, "Aim", roster);
        aimSummaryEncounter.SetTarget(hero.Id, enemy.Id, roster);
        var aimSummary = CombatActionSummary.Create(aimSummaryEncounter, hero, roster, loadout)!;
        Check(
            !aimSummary.Rows.Single(r => r.Label == "Target").Value.Contains("Range"),
            "Summary hides range until ranged attack selected"
        );
        var combinedAim = Fresh();
        combinedAim.ChooseAction(hero.Id, "Aim", roster);
        combinedAim.ChooseAction(hero.Id, "Aim", roster);
        combinedAim.ResolveActions(1, roster);
        combinedAim.Next(roster);
        combinedAim.Next(roster);
        combinedAim.ChooseAction(hero.Id, "Ranged Attack", roster);
        combinedAim.ChooseAction(hero.Id, "Aim", roster, 3);
        var combinedSummary = CombatActionSummary.Create(combinedAim, hero, roster, loadout)!;
        Check(
            combinedSummary.Rows.Single(r => r.Label == "Aim").Value
                == "Ranged Attack +2, Carried Over +1",
            "Combined Aim summary"
        );
        combinedAim.ResetActions(hero.Id, roster);
        Check(
            CombatActionSummary
                .Create(combinedAim, hero, roster, loadout)!
                .Rows.Single(r => r.Label == "Aim")
                .Value == "Carried Over +2",
            "Zero ranged bonus omitted"
        );
        Check(!summary.Rows.Any(r => r.Label == "Aim"), "No Aim row for zero bonuses");
        var attackerA = new SceneCombatant(
            101,
            101,
            "Attacker A",
            false,
            false,
            true,
            0,
            0,
            "777777"
        );
        var attackerB = new SceneCombatant(
            102,
            102,
            "Attacker B",
            false,
            false,
            true,
            2,
            0,
            "777777"
        );
        var defender = new SceneCombatant(103, 103, "Defender", true, false, true, 4, 2, "777777");
        var reactionRoster = new[] { attackerA, attackerB, defender };
        var reactions = new Encounter();
        reactions.Begin(
            reactionRoster,
            true,
            true,
            0,
            new Dictionary<int, int> { [101] = 20, [102] = 10 },
            () => 7
        );
        reactions.ChooseAction(101, "Ranged Attack", reactionRoster);
        reactions.SetTarget(101, 103, reactionRoster);
        reactions.SetTargetReaction(101, "Dodge", reactionRoster);
        reactions.SetTargetReaction(101, "Dodge", reactionRoster);
        Check(
            reactions.ActionFor(103).ReactionCount == 0
                && reactions.MovementRanges(reactionRoster).Count == 0,
            "Dodge selection remains pending and has no dive circle"
        );
        reactions.SetTargetReaction(101, "Parry", reactionRoster);
        Check(
            reactions.ActionFor(103).ReactionCount == 0,
            "Editing a reaction does not double count"
        );
        reactions.SetTargetReaction(101, "None", reactionRoster);
        Check(reactions.ActionFor(103).ReactionCount == 0, "None undoes pending reaction");
        reactions.SetTargetReaction(101, "Dive for Cover", reactionRoster);
        var dive = reactions.MovementRanges(reactionRoster).Single();
        Check(
            dive.Id == 103 && dive.Column == 4 && dive.Row == 2 && dive.Diameter == 4.5,
            "Dive circle is three squares centred on target"
        );
        reactions.SetTargetReactionStance(101, "Crouching", reactionRoster);
        var coverSummary = CombatActionSummary.Create(reactions, attackerA, reactionRoster, null)!;
        Check(
            coverSummary.Rows.Single(r => r.Label == "Target Reaction").Value
                == "Dive for Cover (Crouching)"
                && !coverSummary.Rows.Any(r => r.Label == "Target Reaction Stance"),
            "Cover stance combined into reaction summary"
        );
        Check(
            reactions.ActionFor(103).Stance == "Standing"
                && reactions.ActionFor(101).TargetReactionStance == "Crouching",
            "Target reaction stance remains pending"
        );
        reactions.SetTargetReaction(101, "Dive for Prone", reactionRoster);
        Check(
            reactions.ActionFor(103).Stance == "Standing"
                && reactions.ActionFor(101).TargetReactionStance == "Prone"
                && reactions.ActionFor(103).ReactionCount == 0,
            "Dive for Prone defaults to prone without another count"
        );
        var reactionSnapshot = JsonSerializer.Deserialize<EncounterSnapshot>(
            JsonSerializer.Serialize(reactions.Snapshot())
        )!;
        var resumedReactions = new Encounter();
        resumedReactions.Restore(reactionSnapshot, reactionRoster);
        Check(
            resumedReactions.ActionFor(103).ReactionCount == 0
                && resumedReactions.ActionFor(101).TargetReaction == "Dive for Prone"
                && resumedReactions.MovementRanges(reactionRoster).Single().Diameter == 4.5,
            "Reaction state, count, stance and circle survive reload"
        );
        Check(
            !reactions
                .MovementRanges([attackerA, attackerB, defender with { IsVisible = false }])
                .Single()
                .IsVisible,
            "Hidden dive circle not published"
        );
        reactions.ResolveActions(101, reactionRoster);
        reactions.ResolveActions(101, reactionRoster);
        Check(
            reactions.ActionFor(103).Stance == "Prone"
                && reactions.ActionFor(103).ReactionCount == 1,
            "Resolution commits target stance and reaction exactly once"
        );
        reactions.Next(reactionRoster);
        Check(
            reactions.SelectedId == 102
                && reactions.ActionFor(103).ReactionCount == 1
                && reactions.MovementRanges(reactionRoster).Count == 0,
            "Defence penalty survives attacker end; dive circle clears"
        );
        reactions.ChooseAction(102, "Melee Attack", reactionRoster);
        reactions.SetTarget(102, 103, reactionRoster);
        reactions.SetTargetReaction(102, "Parry", reactionRoster);
        reactions.ResolveActions(102, reactionRoster);
        Check(
            reactions.ActionFor(103).ReactionCount == 2,
            "Reactions from multiple attacks accumulate"
        );
        reactions.Next(reactionRoster);
        Check(
            reactions.SelectedId == 103 && reactions.ActionFor(103).ReactionCount == 2,
            "Penalty retained during target's next turn"
        );
        var defenderSummary = CombatActionSummary.Create(
            reactions,
            defender,
            reactionRoster,
            null
        )!;
        Check(
            defenderSummary.Rows.Single(r => r.Label == "Reactions Made").Value
                == "2 Reactions (DM -2)",
            "Cumulative reaction modifier in summary"
        );
        reactions.ResetActions(103, reactionRoster);
        Check(
            reactions.ActionFor(103).ReactionCount == 2,
            "Resetting action choices does not clear reaction penalty"
        );
        reactions.Next(reactionRoster);
        Check(
            reactions.ActionFor(103).ReactionCount == 0
                && CombatActionSummary
                    .Create(reactions, defender, reactionRoster, null)!
                    .Rows.Single(r => r.Label == "Reactions Made")
                    .Value == "None",
            "Penalty clears after target finishes turn"
        );
        reactions.ChooseAction(101, "Ranged Attack", reactionRoster);
        reactions.SetTarget(101, 103, reactionRoster);
        reactions.SetTargetReaction(101, "Dodge", reactionRoster);
        reactions.SetTarget(101, null, reactionRoster);
        Check(
            reactions.ActionFor(103).ReactionCount == 0
                && reactions.ActionFor(101).TargetReaction == "None",
            "Changing target releases pending reaction"
        );
        reactions.SetTarget(101, 103, reactionRoster);
        reactions.SetTargetReaction(101, "Dive for Cover", reactionRoster);
        reactions.ResetActions(101, reactionRoster);
        Check(
            reactions.ActionFor(103).ReactionCount == 0
                && reactions.MovementRanges(reactionRoster).Count == 0,
            "Reset removes pending reaction and circle"
        );
        reactions.ChooseAction(101, "Melee Attack", reactionRoster);
        reactions.SetTargetReaction(101, "Dodge", reactionRoster);
        reactions.ChooseAction(101, "Leadership", reactionRoster);
        Check(
            reactions.ActionFor(103).ReactionCount == 0
                && reactions.ActionFor(101).TargetReaction == "None",
            "Nonattack cancels pending reaction"
        );
        var draft = Fresh();
        draft.ChooseAction(1, "Change Stance", roster);
        draft.SetStance(1, "Prone", roster);
        Check(
            draft.ActionFor(1).Stance == "Standing" && draft.ActionFor(1).SelectedStance == "Prone",
            "Actor stance is a preview until Resolve"
        );
        var draftReload = new Encounter();
        draftReload.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(draft.Snapshot())
            )!,
            roster
        );
        Check(
            draftReload.ActionFor(1).Stance == "Standing"
                && draftReload.ActionFor(1).PendingStance == "Prone",
            "Unresolved stance survives reload without applying"
        );
        draftReload.ResolveActions(1, roster);
        Check(
            draftReload.ActionFor(1).Stance == "Prone" && draftReload.ActionFor(1).ActionsResolved,
            "Resolve applies actor stance"
        );
        draftReload.ResetActions(1, roster);
        Check(
            draftReload.ActionFor(1).Stance == "Prone" && !draftReload.ActionFor(1).ActionsResolved,
            "Reset clears choices while retaining resolved stance"
        );
        var abandoned = Fresh();
        abandoned.ChooseAction(1, "Aim", roster);
        abandoned.Next(roster);
        abandoned.Next(roster);
        Check(abandoned.ActionFor(1).CarriedAimBonus == 0, "Unresolved Aim does not carry");
        var appliedAgain = new Encounter();
        appliedAgain.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(resumedReactions.Snapshot())
            )!,
            reactionRoster
        );
        appliedAgain.ResolveActions(101, reactionRoster);
        var resolvedReload = new Encounter();
        resolvedReload.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(appliedAgain.Snapshot())
            )!,
            reactionRoster
        );
        resolvedReload.ResolveActions(101, reactionRoster);
        Check(
            resolvedReload.ActionFor(103).ReactionCount == 1
                && resolvedReload.ActionFor(103).Stance == "Prone",
            "Resolving after reload cannot double-apply reactions"
        );
        resolvedReload.ResetActions(101, reactionRoster);
        Check(
            resolvedReload.ActionFor(103).ReactionCount == 1
                && resolvedReload.ActionFor(103).Stance == "Prone",
            "Reset does not undo resolved target effects"
        );
        Console.WriteLine(
            "Ordered minor/major columns, Aim-Shoot/Shoot-Aim, sequence persistence, shared counts, cycles and generic Tactics passed."
        );
    }
}
