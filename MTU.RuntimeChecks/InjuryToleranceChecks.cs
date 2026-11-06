using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MTU.Core;
using MTU.Core.Data;

public static class InjuryToleranceChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        var reductions = InjuryTolerance.HalveInjury | InjuryTolerance.MinusOnePerDie;
        Check(
            InjuryToleranceRules.Applicable(
                InjuryTolerance.Kinetic,
                new EquipmentItem { RequiredSkill = "Gun Combat (Slug)" },
                false
            ) == InjuryTolerance.None,
            "Type without reductions has no effect"
        );
        var laser = new EquipmentItem
        {
            Kind = EquipmentKind.Weapon,
            Name = "Laser Rifle",
            RequiredSkill = "Gun Combat (Energy)",
            Damage = "3D",
            Range = 100,
        };
        var slug = laser.Copy();
        slug.Name = "Rifle";
        slug.RequiredSkill = "Gun Combat (Slug)";
        var plasma = laser.Copy();
        plasma.Name = "Plasma Rifle";
        Check(
            InjuryToleranceRules.Apply(13, reductions, 3) == 5,
            "Tolerance subtracts per die, then halves injury rounding down"
        );
        Check(
            InjuryToleranceRules.Apply(3, InjuryTolerance.MinusOnePerDie, 5) == 0
                && InjuryToleranceRules.Apply(5, InjuryTolerance.HalveInjury, 0) == 2,
            "Tolerance never creates negative injury and halves odd injury down"
        );
        Check(
            InjuryToleranceRules.Applicable(reductions, laser, false) == InjuryTolerance.None,
            "No type filters means no reductions"
        );
        Check(
            InjuryToleranceRules.Applicable(reductions | InjuryTolerance.Laser, laser, false)
                == reductions
                && InjuryToleranceRules.Applicable(
                    reductions | InjuryTolerance.Laser,
                    plasma,
                    false
                ) == InjuryTolerance.None,
            "Laser filter matches Laser word only"
        );
        Check(
            InjuryToleranceRules.Applicable(reductions | InjuryTolerance.Energy, laser, false)
                == reductions
                && InjuryToleranceRules.Applicable(
                    reductions | InjuryTolerance.Energy,
                    plasma,
                    false
                ) == reductions,
            "Energy tolerance includes Energy-skill laser weapons"
        );
        Check(
            InjuryToleranceRules.Applicable(reductions | InjuryTolerance.Kinetic, slug, false)
                == reductions
                && InjuryToleranceRules.Applicable(
                    reductions | InjuryTolerance.Kinetic,
                    laser,
                    true
                ) == reductions
                && InjuryToleranceRules.Applicable(
                    reductions | InjuryTolerance.Kinetic,
                    laser,
                    false
                ) == InjuryTolerance.None,
            "Kinetic matches Slug or any Melee attack"
        );
        Check(
            InjuryToleranceRules.Applicable(reductions | InjuryTolerance.Psionic, laser, false)
                == InjuryTolerance.None,
            "Reserved Psionic flag does not match current attacks"
        );
        Check(
            InjuryToleranceRules.Apply(
                13,
                InjuryToleranceRules.Applicable(
                    reductions | InjuryTolerance.Laser | InjuryTolerance.Energy,
                    laser,
                    false
                ),
                3
            ) == 5,
            "Multiple matching filters do not duplicate reductions"
        );
        foreach (
            var sample in new[]
            {
                ("3D", 3),
                ("2d6 + 2", 2),
                ("D", 1),
                ("1DD", 1),
                ("3D x 10", 3),
                ("5", 0),
            }
        )
            Check(
                InjuryToleranceRules.DamageDice(sample.Item1) == sample.Item2,
                "Tolerance damage dice count " + sample.Item1
            );
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var db = new MtuDbContext(
            new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(connection).Options
        );
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261105002402_CoverProtection");
        db.Campaigns.Add(new CampaignEntity { Id = 1, Name = "Tolerance" });
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Characters (Id, CampaignId, Name, PlayerName, Race, Upp, TermsServed, HighestNavalRank, HighestScoutRank, NaturalDr, Inactive, IsNpc) VALUES (1,1,'Existing traveller','','','777777','0',0,0,0,0,0)"
        );
        await migrator.MigrateAsync();
        var characters = new CharacterService(db);
        Check(
            (await characters.ListAsync(1)).Single().InjuryTolerance == InjuryTolerance.None,
            "Existing characters upgrade with no tolerance"
        );
        var traveller = (await characters.ListAsync(1)).Single();
        await characters.SaveAsync(
            traveller with
            {
                InjuryTolerance = reductions | InjuryTolerance.Kinetic,
            }
        );
        Check(
            (await characters.ListAsync(1)).Single().InjuryTolerance
                == (reductions | InjuryTolerance.Kinetic),
            "Traveller flag combinations persist"
        );
        var npcs = new NpcService(db, characters);
        await npcs.SaveAsync(
            new Npc(
                new Character(
                    0,
                    1,
                    "",
                    "Creature",
                    "",
                    "777777",
                    "0",
                    0,
                    0,
                    0,
                    InjuryTolerance: reductions | InjuryTolerance.Laser | InjuryTolerance.Energy
                )
            )
        );
        var npc = (await npcs.ListAsync(1)).Single();
        Check(
            npc.Character.InjuryTolerance
                == (reductions | InjuryTolerance.Laser | InjuryTolerance.Energy),
            "NPC flag combinations persist"
        );
        await npcs.SaveAsync(
            npc with
            {
                Character = npc.Character with { InjuryTolerance = InjuryTolerance.None },
            }
        );
        await characters.SaveAsync(traveller with { InjuryTolerance = InjuryTolerance.None });
        Check(
            (await characters.ListAsync(1)).Single().InjuryTolerance == InjuryTolerance.None
                && (await npcs.ListAsync(1)).Single().Character.InjuryTolerance
                    == InjuryTolerance.None,
            "Both editors can clear tolerance flags"
        );
        var animals = new AnimalService(db);
        await animals.SaveAsync(
            new Animal(
                new Character(
                    0,
                    1,
                    "",
                    "Armoured Beast",
                    "Animal",
                    "",
                    "0",
                    0,
                    0,
                    0,
                    InjuryTolerance: reductions | InjuryTolerance.Kinetic
                ),
                20,
                "Carnivores",
                "Hunter",
                "",
                []
            )
        );
        var animal = (await animals.ListAsync(1)).Single();
        Check(animal.Description == "", "Existing animal description defaults to empty");
        await animals.SaveAsync(animal with { Description = "Striped hide.\nLong tail." });
        animal = (await animals.ListAsync(1)).Single();
        Check(
            animal.Description == "Striped hide.\nLong tail.",
            "Animal multiline description persists"
        );
        Check(
            animal.Character.InjuryTolerance == (reductions | InjuryTolerance.Kinetic),
            "Animal tolerance persists on creation"
        );
        Check(
            InjuryToleranceRules.Apply(
                13,
                InjuryToleranceRules.Applicable(animal.Character.InjuryTolerance, slug, false),
                3
            ) == 5,
            "Animal tolerance uses the same injury reductions as NPCs"
        );
        await animals.SaveAsync(
            animal with
            {
                Character = animal.Character with
                {
                    InjuryTolerance = reductions | InjuryTolerance.Energy | InjuryTolerance.Laser,
                },
            }
        );
        animal = (await animals.ListAsync(1)).Single();
        Check(
            animal.Character.InjuryTolerance
                == (reductions | InjuryTolerance.Energy | InjuryTolerance.Laser),
            "Animal tolerance can be updated"
        );
        await animals.SaveAsync(
            animal with
            {
                Character = animal.Character with { InjuryTolerance = InjuryTolerance.None },
            }
        );
        Check(
            (await animals.ListAsync(1)).Single().Character.InjuryTolerance == InjuryTolerance.None,
            "Animal tolerance can be cleared"
        );
        Check(
            (await animals.ListAsync(1)).Single().Description == "Striped hide.\nLong tail.",
            "Editing animal preserves description"
        );
        await animals.SaveAsync(animal with { Description = "" });
        Check(
            (await animals.ListAsync(1)).Single().Description == "",
            "Animal description can be cleared"
        );
        var actor = new SceneCombatant(1, 1, "Hero", false, false, true, 0, 0, "777777");
        var target = new SceneCombatant(2, 2, "Creature", true, false, true, 1, 0, "777777");
        var roster = new[] { actor, target };
        var e = new Encounter();
        e.Begin(roster, true, true, 0, new Dictionary<int, int> { [1] = 20 }, () => 7);
        e.ChooseAction(1, "Ranged Attack", roster, 2);
        e.SetTarget(1, 2, roster);
        var gear = new CombatLoadout([new() { Equipped = true, Equipment = slug }]);
        var armour = new CombatLoadout([
            new()
            {
                Equipped = true,
                Equipment = new() { Kind = EquipmentKind.Armour, Protection = 2 },
            },
        ]);
        var attack = e.BeginAttack(
            1,
            roster,
            gear,
            armour,
            0,
            reductions | InjuryTolerance.Kinetic
        );
        attack.SetAttackRoll(12);
        attack.SetDamageRoll(13);
        var raw = attack.Outcome!.TotalDamage - 2;
        Check(
            attack.Outcome.InjuryAfterProtection == raw
                && attack.Injury == Math.Max(0, raw - 3) / 2,
            "Attack calculates tolerance only after armour and Effect"
        );
        e.ApplyAttack(attack, roster);
        var restored = new Encounter();
        restored.Restore(
            JsonSerializer.Deserialize<EncounterSnapshot>(JsonSerializer.Serialize(e.Snapshot()))!,
            roster
        );
        Check(
            restored.ActionFor(1).AttackOutcome!.CalculatedInjury == attack.Outcome.CalculatedInjury
                && restored.Hp(target) == 21 - attack.Injury,
            "Tolerance-adjusted injury applies and survives encounter persistence"
        );
        var legacy = JsonSerializer.Deserialize<AttackOutcome>(
            "{\"AttackRoll\":8,\"Modifier\":0,\"DamageRoll\":10,\"OverallProtection\":2,\"ArmourPiercing\":0}"
        )!;
        Check(
            legacy.CalculatedInjury == 8,
            "Old attack outcomes retain original injury without tolerance"
        );
        Console.WriteLine(
            "Injury Tolerance flags, migration, Traveller/NPC persistence, type filters, stacking and confirmed injury passed."
        );
    }
}
