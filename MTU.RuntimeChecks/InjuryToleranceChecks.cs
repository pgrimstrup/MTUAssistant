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
        static InjuryToleranceEffect One(
            InjuryTolerance record,
            EquipmentItem weapon,
            bool melee
        ) => InjuryToleranceRules.Applicable([record], weapon, melee);
        var reductions = InjuryTolerance.HalveInjury | InjuryTolerance.MinusOnePerDie;
        var both = InjuryToleranceEffect.Of(reductions);
        var minus = new InjuryToleranceEffect(1, 0);
        var halve = new InjuryToleranceEffect(0, 1);
        Check(
            One(
                InjuryTolerance.Kinetic,
                new EquipmentItem { RequiredSkill = "Gun Combat (Slug)" },
                false
            ) == InjuryToleranceEffect.None,
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
        var disruptor = laser.Copy();
        disruptor.Name = "Disruptor Pistol";
        var lowerDisruptor = slug.Copy();
        lowerDisruptor.Name = "heavy disruptor";
        Check(
            InjuryToleranceRules.Apply(13, both, 3) == 5,
            "Tolerance subtracts per die, then halves injury rounding down"
        );
        Check(
            InjuryToleranceRules.Apply(3, minus, 5) == 0
                && InjuryToleranceRules.Apply(5, halve, 0) == 2,
            "Tolerance never creates negative injury and halves odd injury down"
        );
        Check(
            One(reductions, laser, false) == InjuryToleranceEffect.None,
            "No type filters means no reductions"
        );
        Check(
            One(reductions | InjuryTolerance.Laser, laser, false) == both
                && One(reductions | InjuryTolerance.Laser, plasma, false)
                    == InjuryToleranceEffect.None
                && One(reductions | InjuryTolerance.Laser, disruptor, false)
                    == InjuryToleranceEffect.None,
            "Laser filter matches Laser word only"
        );
        Check(
            One(reductions | InjuryTolerance.Disruptor, disruptor, false) == both
                && One(reductions | InjuryTolerance.Disruptor, lowerDisruptor, false) == both
                && One(reductions | InjuryTolerance.Disruptor, laser, false)
                    == InjuryToleranceEffect.None
                && One(reductions | InjuryTolerance.Disruptor, plasma, false)
                    == InjuryToleranceEffect.None
                && One(reductions | InjuryTolerance.Disruptor, disruptor, true)
                    == InjuryToleranceEffect.None,
            "Disruptor filter matches the Disruptor word in a ranged weapon's name only"
        );
        Check(
            One(reductions | InjuryTolerance.Energy, laser, false) == both
                && One(reductions | InjuryTolerance.Energy, plasma, false) == both
                && One(reductions | InjuryTolerance.Energy, disruptor, false) == both,
            "Energy tolerance includes Energy-skill laser and disruptor weapons"
        );
        Check(
            One(reductions | InjuryTolerance.Kinetic, slug, false) == both
                && One(reductions | InjuryTolerance.Kinetic, laser, true) == both
                && One(reductions | InjuryTolerance.Kinetic, laser, false)
                    == InjuryToleranceEffect.None,
            "Kinetic matches Slug or any Melee attack"
        );
        Check(
            One(reductions | InjuryTolerance.Psionic, laser, false) == InjuryToleranceEffect.None,
            "Reserved Psionic flag does not match current attacks"
        );
        Check(
            InjuryToleranceRules.Apply(
                13,
                One(reductions | InjuryTolerance.Laser | InjuryTolerance.Energy, laser, false),
                3
            ) == 5,
            "Multiple matching filters in one record do not duplicate reductions"
        );

        // Stacking: every matching record adds its effects.
        var minusKinetic = InjuryTolerance.MinusOnePerDie | InjuryTolerance.Kinetic;
        var halveKinetic = InjuryTolerance.HalveInjury | InjuryTolerance.Kinetic;
        Check(
            InjuryToleranceRules.Applicable([minusKinetic, minusKinetic], slug, false)
                == new InjuryToleranceEffect(2, 0)
                && InjuryToleranceRules.Apply(13, new InjuryToleranceEffect(2, 0), 3) == 7,
            "Two Minus 1 per D records take 2 per die"
        );
        Check(
            InjuryToleranceRules.Applicable([halveKinetic, halveKinetic], slug, false)
                == new InjuryToleranceEffect(0, 2)
                && InjuryToleranceRules.Apply(13, new InjuryToleranceEffect(0, 2), 3) == 3,
            "Two Halve Injury records halve twice, rounding down each time"
        );
        Check(
            InjuryToleranceRules.Apply(13, new InjuryToleranceEffect(2, 2), 3) == 1,
            "Stacked reductions subtract every per-die reduction before halving"
        );
        var mixed = new[]
        {
            reductions | InjuryTolerance.Kinetic,
            InjuryTolerance.MinusOnePerDie | InjuryTolerance.Laser,
            InjuryTolerance.HalveInjury | InjuryTolerance.Disruptor,
        };
        Check(
            InjuryToleranceRules.Applicable(mixed, slug, false) == both
                && InjuryToleranceRules.Applicable(mixed, laser, false) == minus
                && InjuryToleranceRules.Applicable(mixed, disruptor, false) == halve
                && InjuryToleranceRules.Applicable(mixed, plasma, false)
                    == InjuryToleranceEffect.None
                && InjuryToleranceRules.Applicable([], slug, false) == InjuryToleranceEffect.None
                && InjuryToleranceRules.Applicable(null, slug, false) == InjuryToleranceEffect.None,
            "Only the records matching the attack stack"
        );
        Check(
            InjuryToleranceRules.Describe(new InjuryToleranceEffect(2, 2), 3)
                == "Minus 2 per D (3 dice), Halve Injury ×2"
                && InjuryToleranceRules.Describe(both, 3) == "Minus 1 per D (3 dice), Halve Injury",
            "Stacked tolerance is described with its totals"
        );
        Check(
            InjuryToleranceRules.Normalize([InjuryTolerance.None, minusKinetic, halveKinetic])
                is [var first, var second]
                && first == minusKinetic
                && second == halveKinetic,
            "Empty records are dropped and order is kept"
        );
        Check(
            !InjuryToleranceRules.IsComplete(InjuryTolerance.Kinetic)
                && !InjuryToleranceRules.IsComplete(reductions)
                && InjuryToleranceRules.IsComplete(minusKinetic),
            "A record needs an effect and a damage type to do anything"
        );
        static bool Rejects(IReadOnlyList<InjuryTolerance> records)
        {
            try
            {
                InjuryToleranceRules.Validate(records);
                return false;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }
        Check(
            Rejects([(InjuryTolerance)128])
                && Rejects(
                    Enumerable.Repeat(minusKinetic, InjuryToleranceRules.MaxRecords + 1).ToArray()
                )
                && !Rejects(
                    Enumerable.Repeat(minusKinetic, InjuryToleranceRules.MaxRecords).ToArray()
                ),
            "Unknown options and too many records are rejected"
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

        await RecordMigrationAsync(Check);

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
            (await characters.ListAsync(1)).Single().InjuryTolerances is { Count: 0 },
            "Existing characters upgrade with no tolerance"
        );
        static bool Same(
            IReadOnlyList<InjuryTolerance>? actual,
            params InjuryTolerance[] expected
        ) => (actual ?? []).SequenceEqual(expected);
        var traveller = (await characters.ListAsync(1)).Single();
        await characters.SaveAsync(
            traveller with
            {
                InjuryTolerances =
                [
                    reductions | InjuryTolerance.Kinetic,
                    InjuryTolerance.None,
                    InjuryTolerance.MinusOnePerDie | InjuryTolerance.Disruptor,
                ],
            }
        );
        Check(
            Same(
                (await characters.ListAsync(1)).Single().InjuryTolerances,
                reductions | InjuryTolerance.Kinetic,
                InjuryTolerance.MinusOnePerDie | InjuryTolerance.Disruptor
            ),
            "Traveller records persist in order, without empty records"
        );
        await characters.SaveAsync(
            traveller with
            {
                InjuryTolerances = [InjuryTolerance.MinusOnePerDie | InjuryTolerance.Disruptor],
            }
        );
        Check(
            Same(
                (await characters.ListAsync(1)).Single().InjuryTolerances,
                InjuryTolerance.MinusOnePerDie | InjuryTolerance.Disruptor
            ),
            "Saving replaces a Traveller's records"
        );
        var npcs = new NpcService(db, characters);
        var npcId = await npcs.SaveAsync(
            new Npc(
                new Character(
                    0,
                    1,
                    "",
                    "Creature",
                    "",
                    "777777",
                    0,
                    InjuryTolerances:
                    [
                        reductions | InjuryTolerance.Laser | InjuryTolerance.Energy,
                        minusKinetic,
                    ]
                )
            )
        );
        var npc = (await npcs.ListAsync(1)).Single();
        Check(
            Same(
                npc.Character.InjuryTolerances,
                reductions | InjuryTolerance.Laser | InjuryTolerance.Energy,
                minusKinetic
            ),
            "NPC records persist"
        );
        await npcs.SaveAsync(npc with { Character = npc.Character with { InjuryTolerances = [] } });
        await characters.SaveAsync(traveller with { InjuryTolerances = null });
        Check(
            (await characters.ListAsync(1)).Single().InjuryTolerances is { Count: 0 }
                && (await npcs.ListAsync(1)).Single().Character.InjuryTolerances is { Count: 0 },
            "Both editors can clear tolerance records"
        );
        await npcs.SaveAsync(
            npc with
            {
                Character = npc.Character with { InjuryTolerances = [minusKinetic] },
            }
        );
        await npcs.DeleteAsync(1, npcId);
        Check(
            !await db.CharacterInjuryTolerances.AnyAsync(t => t.CharacterId == npcId),
            "Deleting a character deletes its tolerance records"
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
                    0,
                    InjuryTolerances: [reductions | InjuryTolerance.Kinetic]
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
            Same(animal.Character.InjuryTolerances, reductions | InjuryTolerance.Kinetic),
            "Animal tolerance persists on creation and through other edits"
        );
        Check(
            InjuryToleranceRules.Apply(
                13,
                InjuryToleranceRules.Applicable(animal.Character.InjuryTolerances, slug, false),
                3
            ) == 5,
            "Animal tolerance uses the same injury reductions as NPCs"
        );
        await animals.SaveAsync(
            animal with
            {
                Character = animal.Character with
                {
                    InjuryTolerances =
                    [
                        reductions | InjuryTolerance.Energy | InjuryTolerance.Laser,
                        halveKinetic,
                    ],
                },
            }
        );
        animal = (await animals.ListAsync(1)).Single();
        Check(
            Same(
                animal.Character.InjuryTolerances,
                reductions | InjuryTolerance.Energy | InjuryTolerance.Laser,
                halveKinetic
            ),
            "Animal tolerance can be updated"
        );
        await animals.SaveAsync(
            animal with
            {
                Character = animal.Character with { InjuryTolerances = [] },
            }
        );
        Check(
            (await animals.ListAsync(1)).Single().Character.InjuryTolerances is { Count: 0 },
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
        var stacked = e.BeginAttack(
            1,
            roster,
            gear,
            armour,
            0,
            [reductions | InjuryTolerance.Kinetic, minusKinetic, halveKinetic]
        );
        stacked.SetAttackRoll(12);
        stacked.SetDamageRoll(13);
        var stackedRaw = stacked.Outcome!.TotalDamage - 2;
        Check(
            stacked.ApplicableInjuryTolerance == new InjuryToleranceEffect(2, 2)
                && stacked.Injury == Math.Max(0, stackedRaw - 6) / 4,
            "An attack stacks every matching record"
        );
        var attack = e.BeginAttack(
            1,
            roster,
            gear,
            armour,
            0,
            [
                reductions | InjuryTolerance.Kinetic,
                InjuryTolerance.HalveInjury | InjuryTolerance.Laser,
            ]
        );
        attack.SetAttackRoll(12);
        attack.SetDamageRoll(13);
        var raw = attack.Outcome!.TotalDamage - 2;
        Check(
            attack.Outcome.InjuryAfterProtection == raw
                && attack.Injury == Math.Max(0, raw - 3) / 2,
            "Attack calculates tolerance only after armour and Effect, ignoring records that don't match"
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
        var legacyTolerance = JsonSerializer.Deserialize<AttackOutcome>(
            "{\"AttackRoll\":8,\"Modifier\":0,\"DamageRoll\":10,\"OverallProtection\":2,\"ArmourPiercing\":0,\"InjuryTolerance\":3,\"WeaponDamageDice\":3}"
        )!;
        Check(
            legacyTolerance.CalculatedInjury == 2,
            "Attack outcomes saved with a single tolerance keep their reduced injury"
        );
        Console.WriteLine(
            "Injury Tolerance records, Disruptor, stacking, migration, persistence, type filters and confirmed injury passed."
        );
    }

    /// <summary>The single value becomes the first record, and rolling back merges the records
    /// into one value again.</summary>
    private static async Task RecordMigrationAsync(Action<bool, string> check)
    {
        const string previous = "20261122030000_VisualDistances";
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(connection).Options;
        using (var db = new MtuDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync(previous);
            // Insert with SQL: the Characters table still has the single InjuryTolerance column.
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO Campaigns (Id, Name, Day, Year, IsActive) VALUES (1, 'Legacy', 1, 1105, 1);
                INSERT INTO Characters (Id, CampaignId, Name, PlayerName, Race, Upp, NaturalDr, Inactive, IsNpc, IsAnimal, InjuryTolerance, Speed, Description)
                    VALUES (1, 1, 'Armoured', '', '', '777777', 0, 0, 0, 0, 7, 6, ''),
                           (2, 1, 'Plain', '', '', '777777', 0, 0, 1, 0, 0, 6, ''),
                           (3, 1, 'Beast', '', 'Animal', '', 0, 0, 0, 1, 19, 6, '');
                """
            );
            await db.Database.MigrateAsync();
            Task<int[]> Records(int id) =>
                db
                    .Database.SqlQueryRaw<int>(
                        "SELECT Tolerance AS Value FROM CharacterInjuryTolerances WHERE CharacterId = {0} ORDER BY Sequence",
                        id
                    )
                    .ToArrayAsync();
            check(
                (await Records(1)).SequenceEqual([7])
                    && (await Records(2)).Length == 0
                    && (await Records(3)).SequenceEqual([19]),
                "Each existing Injury Tolerance becomes the character's first record"
            );
            check(
                await db
                    .Database.SqlQueryRaw<int>(
                        "SELECT COUNT(*) AS Value FROM pragma_table_info('Characters') WHERE name = 'InjuryTolerance'"
                    )
                    .SingleAsync() == 0,
                "The single Injury Tolerance column is removed"
            );
            var travellers = await new CharacterService(db).ListAsync(1);
            check(
                travellers.Single().InjuryTolerances is [var only] && (int)only == 7,
                "The converted record loads through the Traveller service"
            );
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO CharacterInjuryTolerances (CharacterId, Sequence, Tolerance) VALUES (1, 2, 82)"
            );
            await db.GetService<IMigrator>().MigrateAsync(previous);
            check(
                (
                    await db
                        .Database.SqlQueryRaw<int>(
                            "SELECT InjuryTolerance AS Value FROM Characters ORDER BY Id"
                        )
                        .ToArrayAsync()
                ).SequenceEqual([7 | 16, 0, 19]),
                "Rolling back merges the records into one value and drops Disruptor"
            );
            await db.Database.MigrateAsync();
        }
        using (var db = new MtuDbContext(options))
            check(
                (await new CharacterService(db).ListAsync(1)).Single().InjuryTolerances
                    is [var merged]
                    && merged == (InjuryTolerance)(7 | 16),
                "Migrating up again restores the merged value as a record"
            );
    }
}
