using System.Text.Json;
using MTU.Core;

public static class AttackResolutionChecks
{
    public static void Run()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        static void Reject(Action action)
        {
            try
            {
                action();
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new Exception("Invalid attack operation accepted.");
        }
        Check(
            new AttackOutcome(10, -4, null, 0, 0).Summary == "Rolled 10 - 4 → 6 is a Miss",
            "Player miss formatting"
        );
        Check(
            new AttackOutcome(3, 10, null, 0, 0).Summary
                == "Rolled 3 + 10 → 13 is a Hit (Effect 5)",
            "Player hit and Effect formatting"
        );
        Check(
            new AttackOutcome(3, 10, 7, 0, 0).Summary.Contains("Rolled 7 + Effect 5 → 12 damage"),
            "Effect added to damage"
        );
        Check(
            new AttackOutcome(8, 0, 2, 20, 50).RemainingProtection == 0
                && new AttackOutcome(8, 0, 2, 20, 50).CalculatedInjury == 2,
            "AP cannot reduce protection below zero"
        );
        Check(
            new AttackOutcome(8, 0, 2, 20, 0).CalculatedInjury == 0,
            "Protection cannot produce negative injury"
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
            "777777",
            Skills: [new(0, 1, "Gun Combat (Slug)", 2), new(0, 1, "Melee (Blade)", 2)]
        );
        var target = new SceneCombatant(2, 2, "Foe", true, false, true, 1, 0, "876777");
        var roster = new[] { actor, target };
        var gun = new CharacterEquipmentDetails
        {
            Equipped = true,
            Equipment = new()
            {
                Kind = EquipmentKind.Weapon,
                Name = "Rifle",
                Damage = "3D",
                Range = 100,
                RequiredSkill = "Gun Combat (Slug)",
                Traits = "Auto 2, AP 5",
            },
        };
        var sword = new CharacterEquipmentDetails
        {
            Equipped = true,
            Equipment = new()
            {
                Kind = EquipmentKind.Weapon,
                Name = "Sword",
                Damage = "2D+2",
                RequiredSkill = "Melee (Blade)",
                Traits = "AP (5)",
            },
        };
        var loadout = new CombatLoadout([gun, sword]);
        var armour = new CombatLoadout([
            new()
            {
                Equipped = true,
                Equipment = new() { Kind = EquipmentKind.Armour, Protection = 4 },
            },
            new()
            {
                Equipped = true,
                Equipment = new() { Kind = EquipmentKind.Armour, Protection = 2 },
            },
            new()
            {
                Equipped = false,
                Equipment = new() { Kind = EquipmentKind.Armour, Protection = 100 },
            },
        ]);
        Encounter Fresh(string action = "Ranged Attack", bool hiding = true)
        {
            var e = new Encounter();
            e.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
            e.SetCoverStatus(2, true, true, roster);
            e.SetCoverProtection(2, new CombatCover(1, "Wall", 8, 24), roster);
            e.ResolveActions(2, roster);
            if (!hiding)
            {
                e.ResetActions(2, roster);
                e.SetCoverStatus(2, true, false, roster);
                e.ResolveActions(2, roster);
            }
            e.ChooseAction(1, action, roster, 2);
            e.SetTarget(1, 2, roster);
            return e;
        }
        var encounter = Fresh();
        var attack = encounter.BeginAttack(1, roster, loadout, armour, 3);
        Check(
            !attack.NpcMode && attack.AttackRoll is null && !attack.Ready,
            "Player attack awaits rolls"
        );
        Check(
            attack.OverallProtection == 17 && attack.ArmourPiercing == 5,
            "Equipped armour layers, Natural DR and Hiding cover stack before AP"
        );
        Reject(() => attack.SetAttackRoll(1));
        Reject(() => attack.SetDamageRoll(7));
        attack.SetAttackRoll(10);
        encounter.PreviewAttack(1, attack.Outcome);
        Check(
            encounter.Hp(target) == 21 && !encounter.ActionFor(1).ActionsResolved && !attack.Ready,
            "Hit preview does not resolve or injure target"
        );
        Check(
            CombatActionSummary
                .Create(encounter, actor, roster, loadout, true)!
                .Rows.Single(r => r.Label == "Attack Result")
                .Value.Contains("is a Hit"),
            "Cast sees live hit preview"
        );
        encounter.PreviewAttack(1, null);
        Check(
            CombatActionSummary
                .Create(encounter, actor, roster, loadout)!
                .Rows.Single(r => r.Label == "Attack Result")
                .Value.Contains("pending")
                && encounter.ActionFor(2).ReactionCount == 0,
            "Cancellation restores Pending without health or reaction changes"
        );
        attack.SetDamageRoll(12);
        Check(
            attack.Outcome!.TotalDamage == 12 + attack.Outcome.Effect
                && attack.Outcome.RemainingProtection == 12
                && attack.Ready,
            "Player damage and remaining protection calculated"
        );
        attack.SetInjury(10);
        encounter.ApplyAttack(attack, roster);
        Check(
            encounter.Health(target) is { Endurance: 0, Strength: 4, Dexterity: 7 },
            "Confirmed override uses END then higher STR/DEX damage rules"
        );
        Check(
            encounter.ActionFor(1).AttackOutcome!.AppliedInjury == 10
                && encounter.ActionFor(1).TargetHasBeenHit,
            "Confirmed outcome and hit tagging recorded"
        );
        foreach (var playerView in new[] { false, true })
            Check(
                !CombatActionSummary
                    .Create(encounter, actor, roster, loadout, playerView)!
                    .Rows.Single(r => r.Label == "Damage Result")
                    .Value.Contains("injury applied"),
                "NPC injury amounts omitted from GM and Cast summaries"
            );
        var playerTarget = target with { IsNpc = false };
        var npcActor = actor with { IsNpc = true };
        Check(
            CombatActionSummary
                .Create(encounter, npcActor, [npcActor, playerTarget], loadout, true)!
                .Rows.Single(r => r.Label == "Damage Result")
                .Value.Contains("10 injury applied"),
            "Player target injury remains visible"
        );
        Reject(() => encounter.ApplyAttack(attack, roster));
        var restored = new Encounter();
        restored.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(encounter.Snapshot())
            )!,
            roster
        );
        Check(
            restored.ActionFor(1).AttackOutcome == encounter.ActionFor(1).AttackOutcome
                && restored.Hp(target) == 11,
            "Outcome and injury survive encounter persistence"
        );
        restored.ResetActions(1, roster);
        Check(restored.ActionFor(1).AttackOutcome is null, "Reset clears prior attack result");
        var npcEncounter = Fresh();
        var npc = npcEncounter.BeginAttack(1, roster, loadout, armour, 3);
        var rolls = 0;
        npc.SetMode(
            true,
            sides =>
            {
                rolls++;
                return 6;
            }
        );
        Check(
            npc.AttackRoll == 12
                && npc.DamageRoll == 18
                && npc.Ready
                && rolls == 5
                && npcEncounter.Hp(target) == 21,
            "NPC automatically rolls 2D6 and weapon damage but waits for GM confirmation"
        );
        npc.SetMode(
            true,
            sides =>
            {
                rolls++;
                return 1;
            }
        );
        Check(
            npc.Outcome is { Hit: false }
                && npc.DamageRoll is null
                && npc.Injury == 0
                && npc.Ready
                && rolls == 9,
            "NPC miss stops before rolling damage"
        );
        npcEncounter.ApplyAttack(npc, roster);
        Check(
            npcEncounter.Hp(target) == 21 && !npcEncounter.ActionFor(1).TargetHasBeenHit,
            "Miss resolves without damage or hit flag"
        );
        var meleeEncounter = Fresh("Melee Attack", false);
        var melee = meleeEncounter.BeginAttack(1, roster, loadout, armour, 3);
        Check(
            melee.Weapon.Name == "Sword"
                && melee.OverallProtection == 9
                && melee.ArmourPiercing == 5,
            "Melee uses selected weapon and Behind Cover grants no protection"
        );
        melee.SetAttackRoll(12);
        melee.SetDamageRoll(20);
        melee.SetInjury(0);
        meleeEncounter.ApplyAttack(melee, roster);
        Check(
            meleeEncounter.Hp(target) == 21 && meleeEncounter.ActionFor(1).ActionsResolved,
            "GM may override injury to zero"
        );
        foreach (
            var pair in new[]
            {
                ("2D+2", 14),
                ("1DD", 60),
                ("D6", 6),
                ("3D x 10", 180),
                ("5", 5),
                ("1D-9", 0),
            }
        )
            Check(
                AttackResolution.TryRollDamage(pair.Item1, _ => 6, out var damage)
                    && damage == pair.Item2,
                "NPC damage expression " + pair.Item1
            );
        Check(
            !AttackResolution.TryRollDamage("special", _ => 6, out _),
            "Unknown NPC damage can be entered manually"
        );
        var autoPlayerEncounter = Fresh();
        var autoPlayer = autoPlayerEncounter.BeginAttack(1, roster, loadout, armour, 3);
        autoPlayer.AutoRoll(_ => 6);
        Check(
            !autoPlayer.NpcMode
                && autoPlayer.AttackRoll == 12
                && autoPlayer.DamageRoll == 18
                && autoPlayer.Ready,
            "Auto Roll handles player attack and damage without changing mode"
        );
        autoPlayer.SetInjury(50);
        autoPlayer.AutoRoll(_ => 1);
        Check(
            autoPlayer.Outcome is { Hit: false }
                && autoPlayer.DamageRoll is null
                && autoPlayer.Injury == 0,
            "Rerolling clears prior damage and injury on a miss"
        );
        autoPlayerEncounter.PreviewAttack(1, autoPlayer.Outcome);
        var missRows = CombatActionSummary
            .Create(autoPlayerEncounter, actor, roster, loadout, true)!
            .Rows;
        Check(
            missRows.Any(r => r.Label == "Attack Result" && r.Value.Contains("Mishap"))
                && !missRows.Any(r => r.Label == "Damage Result"),
            "Miss summary shows Attack Result only"
        );
        autoPlayer.AutoRoll(_ => 6);
        autoPlayerEncounter.PreviewAttack(1, autoPlayer.Outcome);
        var hitRows = CombatActionSummary
            .Create(autoPlayerEncounter, actor, roster, loadout, true)!
            .Rows;
        Check(
            hitRows.Single(r => r.Label == "Attack Result").Value.Contains("Hit")
                && !hitRows.Single(r => r.Label == "Attack Result").Value.Contains("damage")
                && hitRows.Single(r => r.Label == "Damage Result").Value.Contains("18 + Effect"),
            "Hit summary splits Attack Result and Damage Result in GM and Cast payload"
        );
        var colourEncounter = Fresh(hiding: false);
        var colourAttack = colourEncounter.BeginAttack(1, roster, loadout, null, 0);
        Check(
            colourAttack.TargetOriginalHp == 21
                && colourAttack.TargetRemainingHp == 21
                && colourAttack.CalculatedInjuryColour is null,
            "Injury colours capture original and remaining target HP; pending has no colour"
        );
        colourAttack.SetAttackRoll(7);
        Check(colourAttack.Outcome!.Effect == 0, "Colour test uses zero Effect");
        foreach (
            var sample in new (int Injury, string? Colour)[]
            {
                (0, "LimeGreen"),
                (6, null),
                (7, "Yellow"),
                (13, "Yellow"),
                (14, "Orange"),
                (21, "Orange"),
                (22, "#ff8080"),
            }
        )
        {
            colourAttack.SetDamageRoll(sample.Injury);
            Check(
                colourAttack.CalculatedInjuryColour == sample.Colour,
                "Calculated injury colour at " + sample.Injury
            );
        }
        colourAttack.SetInjury(0);
        Check(
            colourAttack.CalculatedInjuryColour == "#ff8080",
            "GM override does not change calculated-injury colour"
        );
        colourAttack.SetAttackRoll(2);
        Check(colourAttack.CalculatedInjuryColour == "LimeGreen", "Miss is LimeGreen");
        colourEncounter.SetHp(2, 5, roster);
        var wounded = colourEncounter.BeginAttack(1, roster, loadout, null, 0);
        wounded.SetAttackRoll(7);
        wounded.SetDamageRoll(6);
        Check(
            wounded.CalculatedInjuryColour == "#ff8080",
            "Remaining HP takes precedence over original-HP thresholds"
        );
        wounded.SetDamageRoll(0);
        Check(
            wounded.CalculatedInjuryColour == "LimeGreen",
            "Zero injury remains LimeGreen on wounded targets"
        );
        var specialSuit = new EquipmentItem
        {
            Kind = EquipmentKind.Armour,
            Protection = 8,
            ProtectionL = 12,
            ProtectionE = 4,
            ProtectionK = 2,
            ProtectionP = 99,
        };
        var specialCoat = new EquipmentItem
        {
            Kind = EquipmentKind.Armour,
            Protection = 3,
            ProtectionE = 0,
            ProtectionP = 99,
        };
        var specialArmour = new CombatLoadout([
            new() { Equipped = true, Equipment = specialSuit },
            new() { Equipped = true, Equipment = specialCoat },
            new()
            {
                Equipped = false,
                Equipment = new()
                {
                    Kind = EquipmentKind.Armour,
                    ProtectionL = 99,
                    ProtectionE = 99,
                    ProtectionK = 99,
                },
            },
        ]);
        var laser = new EquipmentItem
        {
            Kind = EquipmentKind.Weapon,
            Name = "Laser Rifle",
            RequiredSkill = "Gun Combat (Energy)",
            Range = 100,
            Damage = "3D",
            Traits = "AP 5",
        };
        Check(
            specialArmour.ArmourProtectionFor(laser, false) == 12,
            "Laser override takes precedence per layer; missing laser override falls back to Energy including explicit zero"
        );
        var plasma = laser.Copy();
        plasma.Name = "Plasma Rifle";
        Check(
            specialArmour.ArmourProtectionFor(plasma, false) == 4,
            "Non-laser Energy uses Energy override"
        );
        var slugWeapon = laser.Copy();
        slugWeapon.Name = "Rifle";
        slugWeapon.RequiredSkill = "Gun Combat (Slug)";
        Check(
            specialArmour.ArmourProtectionFor(slugWeapon, false) == 5,
            "Slug uses Kinetic override with base fallback per layer"
        );
        Check(
            specialArmour.ArmourProtectionFor(laser, true) == 5,
            "Melee uses Kinetic regardless of weapon name or skill"
        );
        var archaic = laser.Copy();
        archaic.Name = "Bow";
        archaic.RequiredSkill = "Gun Combat (Archaic)";
        Check(
            specialArmour.ArmourProtectionFor(archaic, false) == 11,
            "Other weapons use base protection and ignore reserved Psionics"
        );
        var nonEnergyLaser = laser.Copy();
        nonEnergyLaser.RequiredSkill = null;
        Check(
            specialArmour.ArmourProtectionFor(nonEnergyLaser, false) == 15,
            "Laser name selects Laser override without Energy skill; other layer falls back to base"
        );
        var lowerCaseLaser = laser.Copy();
        lowerCaseLaser.Name = "laser carbine";
        lowerCaseLaser.RequiredSkill = "gun combat (energy)";
        Check(
            specialArmour.ArmourProtectionFor(lowerCaseLaser, false) == 12,
            "Laser and required skill matching is case insensitive"
        );
        var substring = laser.Copy();
        substring.Name = "Blaser";
        Check(
            specialArmour.ArmourProtectionFor(substring, false) == 4,
            "Laser must be a whole word in the weapon name"
        );
        specialSuit.ProtectionL = 0;
        Check(
            specialArmour.ArmourProtectionFor(laser, false) == 0,
            "Explicit zero Laser overrides both general and Energy protection"
        );
        specialSuit.ProtectionL = null;
        Check(
            specialArmour.ArmourProtectionFor(laser, false) == 4,
            "Null Laser falls back to Energy"
        );
        specialSuit.ProtectionE = null;
        Check(
            specialArmour.ArmourProtectionFor(laser, false) == 8,
            "Null Laser and Energy fall back to base"
        );
        specialSuit.ProtectionK = 0;
        Check(
            specialArmour.ArmourProtectionFor(slugWeapon, false) == 3
                && specialArmour.ArmourProtectionFor(archaic, true) == 3,
            "Explicit zero Kinetic overrides base for Slug and Melee"
        );
        specialSuit.ProtectionL = 12;
        specialSuit.ProtectionE = 4;
        specialSuit.ProtectionK = 2;
        var laserLoadout = new CombatLoadout([new() { Equipped = true, Equipment = laser }]);
        var specialEncounter = Fresh();
        var specialAttack = specialEncounter.BeginAttack(1, roster, laserLoadout, specialArmour, 3);
        specialAttack.SetAttackRoll(12);
        specialAttack.SetDamageRoll(30);
        Check(
            specialAttack.OverallProtection == 23
                && specialAttack.Outcome!.RemainingProtection == 18
                && specialAttack.Outcome.CalculatedInjury == 15,
            "Attack resolution sums selected special protection, Natural DR and Hiding cover, then AP and injury"
        );
        Check(
            Fresh("Melee Attack")
                .BeginAttack(1, roster, loadout, specialArmour, 3)
                .OverallProtection == 16,
            "Melee attack resolution uses Kinetic protection with the same Natural DR and cover additions"
        );
        Check(
            !new AttackOutcome(2, 6, null, 0, 0).Hit && !new AttackOutcome(2, 12, null, 0, 0).Hit,
            "Natural 2 always misses even when modifiers reach the hit threshold"
        );
        Check(
            !new AttackOutcome(2, 6, null, 0, 0).IsMishap
                && new AttackOutcome(2, 5, null, 0, 0).IsMishap,
            "Natural 2 is a simple miss at DM +6 and a mishap below +6"
        );
        var mishapEncounter = Fresh();
        var mishapAttack = mishapEncounter.BeginAttack(1, roster, loadout, armour, 3);
        mishapAttack.SetAttackRoll(2, _ => 3);
        Check(
            mishapAttack.Mishap is { Roll: 6, SkillDm: 2, IntelligenceDm: 0, Total: 8 }
                && mishapAttack.Outcome is { Hit: false, IsMishap: true }
                && mishapAttack.Ready,
            "Mishap rolls 2D6 plus selected firearm skill and INT DM, then stops before damage"
        );
        var description = mishapAttack.Outcome!.AttackSummary;
        Check(
            description.Contains("Mishap check") && description.Contains("workshop"),
            "Attack Result includes brief mishap description"
        );
        mishapEncounter.ApplyAttack(mishapAttack, roster);
        var mishapRestore = new Encounter();
        mishapRestore.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(
                JsonSerializer.Serialize(mishapEncounter.Snapshot())
            )!,
            roster
        );
        Check(
            mishapRestore.ActionFor(1).AttackOutcome!.AttackSummary == description
                && mishapRestore.Hp(target) == 21,
            "Mishap roll and description persist without inflicting damage on the original target"
        );
        var quickActor = actor with
        {
            Upp = "777C77",
            Skills = [new(0, 1, "Gun Combat (Slug)", 2), new(0, 1, "Gun Combat (Energy)", 5)],
        };
        var quickRoster = new[] { quickActor, target };
        var quickEncounter = new Encounter();
        quickEncounter.Begin(
            quickRoster,
            true,
            true,
            0,
            new Dictionary<int, int> { [1] = 20 },
            () => 7
        );
        quickEncounter.ChooseAction(1, "Ranged Attack", quickRoster, 2);
        quickEncounter.SetTarget(1, 2, quickRoster);
        var quickAttack = quickEncounter.BeginAttack(1, quickRoster, loadout, null, 0);
        quickAttack.SetAttackRoll(2, _ => 3);
        Check(
            quickAttack.Mishap is { SkillDm: 2, IntelligenceDm: 2, Total: 10 },
            "Mishap uses INT and the selected weapon skill, not DEX or the highest unrelated skill"
        );
        var meleeMishap = Fresh("Melee Attack").BeginAttack(1, roster, loadout, armour, 3);
        meleeMishap.SetAttackRoll(2, _ => 3);
        Check(
            meleeMishap.Mishap!.SkillDm == 2,
            "Melee mishaps use the relevant Melee specialization"
        );
        meleeMishap.SetAttackRoll(12);
        Check(
            meleeMishap.Mishap is null && !meleeMishap.Outcome!.AttackSummary.Contains("Mishap"),
            "A new attack roll clears the old mishap"
        );
        foreach (
            var sample in new[]
            {
                (-2, "Self-hit"),
                (2, "Self-hit"),
                (3, "out of reach"),
                (4, "out of reach"),
                (5, "in sight"),
                (6, "in sight"),
                (7, "Easy (6+)"),
                (8, "workshop"),
                (9, "workshop"),
                (10, "destroyed"),
                (11, "destroyed"),
                (12, "no further"),
                (16, "no further"),
            }
        )
            Check(
                new CombatMishap(sample.Item1, 0, 0).Description.Contains(sample.Item2),
                "Mishap table band " + sample.Item1
            );
        var expertActor = actor with
        {
            Upp = "7FF777",
            Skills = [new(0, 1, "Gun Combat (Slug)", 5)],
        };
        var expertRoster = new[] { expertActor, target };
        var expertEncounter = new Encounter();
        expertEncounter.Begin(
            expertRoster,
            true,
            true,
            0,
            new Dictionary<int, int> { [1] = 20 },
            () => 7
        );
        expertEncounter.ChooseAction(1, "Ranged Attack", expertRoster, 2);
        expertEncounter.SetTarget(1, 2, expertRoster);
        var expertAttack = expertEncounter.BeginAttack(1, expertRoster, loadout, null, 0);
        var expertDice = 0;
        expertAttack.AutoRoll(_ =>
        {
            expertDice++;
            return 1;
        });
        Check(
            expertAttack.Modifiers.Total >= 6
                && expertAttack.Outcome is { Hit: false, IsMishap: false }
                && expertAttack.Mishap is null
                && expertDice == 2,
            "High-DM natural 2 rolls neither damage nor mishap dice"
        );
        Console.WriteLine(
            "Player/NPC attack rolls, Effect, armour stacking/AP, injury confirmation, cancellation and persistence passed."
        );
    }
}
