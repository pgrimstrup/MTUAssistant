using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MTU.Core;
using MTU.Core.Data;

public static class NpcGenerationChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
        static Func<int> Dice(params int[] rolls)
        {
            var queue = new Queue<int>(rolls);
            return () => queue.Dequeue();
        }
        static Func<int> GenerationDice(
            int tens,
            int units,
            int race,
            int gender,
            NpcSkillLevel level = NpcSkillLevel.Novice
        )
        {
            var rolls = new List<int> { tens, units, race, gender };
            for (var i = 0; i < NpcGenerator.SkillRollCount(level); i++)
            {
                var total = 2 + i % 11;
                var first = Math.Min(6, total - 1);
                rolls.Add(first);
                rolls.Add(total - first);
            }
            rolls.Add(6);
            rolls.AddRange(Enumerable.Repeat(1, 32));
            return Dice(rolls.ToArray());
        }
        Check(
            NpcGenerator.Archetypes.Count == 36
                && NpcGenerator.Archetypes.Select(a => a.D66).Distinct().Count() == 36,
            "All 36 D66 types remain available"
        );
        Check(
            NpcGenerator.PatronArchetypes.Count == 36
                && NpcGenerator.PatronArchetypes.Select(a => a.D66).Distinct().Count() == 36,
            "Patron table covers all 36 D66 outcomes"
        );
        Check(
            NpcGenerator
                .PatronArchetypes.Select(a => a.Occupation)
                .SequenceEqual(
                    new[]
                    {
                        "Assassin",
                        "Smuggler",
                        "Terrorist",
                        "Embezzler",
                        "Thief",
                        "Revolutionary",
                        "Clerk",
                        "Administrator",
                        "Mayor",
                        "Minor Noble",
                        "Physician",
                        "Tribal Leader",
                        "Diplomat",
                        "Courier",
                        "Spy",
                        "Ambassador",
                        "Noble",
                        "Police Officer",
                        "Merchant",
                        "Free Trader",
                        "Broker",
                        "Corporate Executive",
                        "Corporate Agent",
                        "Financier",
                        "Belter",
                        "Researcher",
                        "Naval Officer",
                        "Pilot",
                        "Starport Administrator",
                        "Scout",
                        "Alien",
                        "Playboy",
                        "Stowaway",
                        "Family Relative",
                        "Agent of a Foreign Power",
                        "Imperial Agent",
                    }
                ),
            "Patron occupations match the supplied table exactly"
        );
        foreach (
            var archetype in NpcGenerator
                .Archetypes.Concat(NpcGenerator.PatronArchetypes)
                .Append(NpcGenerator.CombatantArchetype)
        )
        {
            Check(
                archetype.Skills.Count == 11
                    && archetype.Skills.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 11,
                "Every profession has eleven distinct skills"
            );
            foreach (var name in archetype.Skills)
            {
                var (skill, speciality) = CharacterSkillCatalogue.Parse(name);
                Check(
                    skill is not null
                        && (
                            skill.Specialities.Count == 0
                                ? speciality is null
                                : speciality is not null && skill.Specialities.Contains(speciality)
                        ),
                    $"Catalogue skill required: {name}"
                );
            }
            for (var total = 2; total <= 12; total++)
            {
                var first = Math.Min(6, total - 1);
                var result = NpcGenerator.GenerateSkills(
                    archetype,
                    NpcSkillLevel.Recruit,
                    Dice(first, total - first, first, total - first)
                );
                Check(
                    result.Single().Name == archetype.Skills[total - 2]
                        && result.Single().Level == 1,
                    "2D6 totals map directly to slots 2-12, with no selection DMs"
                );
            }
        }
        for (var tens = 1; tens <= 6; tens++)
        for (var units = 1; units <= 6; units++)
        {
            var npc = NpcGenerator.Generate(1, d6: GenerationDice(tens, units, 1, 1));
            Check(
                npc.Occupation
                    == NpcGenerator.Archetypes.Single(a => a.D66 == $"{tens}{units}").Occupation,
                "Occupation still uses ordered D66 dice"
            );
            var patron = NpcGenerator.Generate(
                1,
                d6: GenerationDice(tens, units, 1, 1),
                type: NpcType.PatronCivilian
            );
            Check(
                patron.Type == NpcType.PatronCivilian
                    && NpcGenerator.ArchetypeFor(patron).D66 == $"{tens}{units}",
                "Patron generation uses its own D66 table and type"
            );
        }
        for (var race = 1; race <= 6; race++)
        for (var gender = 1; gender <= 6; gender++)
        {
            var npc = NpcGenerator.Generate(1, d6: GenerationDice(1, 1, race, gender));
            Check(
                npc.Character.Race
                    == (
                        race <= 3 ? "Human"
                        : race <= 5 ? "Aslan"
                        : "Vargr"
                    )
                    && npc.Gender == (gender <= 3 ? "Male" : "Female"),
                "Race and gender boundaries remain correct"
            );
            Check(
                npc.Character.Upp.Length == 6
                    && npc.Character.Upp.All(c => "3456789ABC".Contains(c))
                    && npc.Quirks1 == "Loyal"
                    && npc.Quirks2 == ""
                    && !string.IsNullOrWhiteSpace(npc.Character.Name),
                "Generated UPP respects limits, names are filled and duplicate quirk rolls give one quirk"
            );
        }
        var naval = NpcGenerator.Archetypes[0];
        Check(
            naval.Skills[5] == "Pilot (Spacecraft)"
                && NpcGenerator.Archetypes.Single(a => a.D66 == "14").Skills[5] == "Medic",
            "Primary skills sit at the most likely roll of seven"
        );
        foreach (var level in Enum.GetValues<NpcSkillLevel>())
        {
            var npc = NpcGenerator.Generate(1, level, GenerationDice(1, 1, 1, 1, level));
            Check(
                npc.Character.Skills!.Sum(s => s.Level + 1) == 2 * ((int)level + 1)
                    && npc.Character.Skills!.All(s => s.Level <= 5),
                "Every level receives its full skill budget within the rank cap"
            );
            Check(
                NpcGenerator.GenerateAge(level, () => 1) == 16 + 4 * ((int)level + 1) - 2
                    && NpcGenerator.GenerateAge(level, () => 6) == 16 + 4 * ((int)level + 1) + 3,
                "Age formula unchanged"
            );
        }
        var cappedRolls = Enumerable
            .Range(0, 7)
            .SelectMany(_ => new[] { 3, 4 })
            .Concat(new[] { 1, 1, 1, 2, 1, 3, 1, 4 })
            .ToArray();
        var calls = 0;
        var cappedDice = Dice(cappedRolls);
        var capped = NpcGenerator.GenerateSkills(
            naval,
            NpcSkillLevel.Veteran,
            () =>
            {
                calls++;
                return cappedDice();
            }
        );
        Check(
            capped.Single(s => s.Name == "Pilot (Spacecraft)").Level == 5
                && capped.Sum(s => s.Level + 1) == 10
                && calls == 22,
            "Six acquisitions reach rank five; a seventh is rerolled without consuming the skill budget"
        );
        Check(
            Enum.GetNames<NpcSkillLevel>()
                .SequenceEqual(
                    new[]
                    {
                        "Recruit",
                        "Novice",
                        "Adept",
                        "Practitioner",
                        "Veteran",
                        "Expert",
                        "Master",
                        "Elite",
                        "Champion",
                        "Hero",
                        "Mythic",
                        "Legendary",
                    }
                ),
            "Requested skill level ordering retained"
        );
        Check(
            NpcGenerator.GenerateAge(NpcSkillLevel.Legendary, () => 6) == 67,
            "Maximum age remains 67"
        );
        try
        {
            NpcGenerator.Generate(1, d6: () => 7);
            throw new Exception("Invalid die accepted");
        }
        catch (ArgumentException) { }
        try
        {
            NpcGenerator.SkillRollCount((NpcSkillLevel)99);
            throw new Exception("Invalid level accepted");
        }
        catch (ArgumentException) { }
        var biased = NpcGenerator.GenerateSkills(
            naval,
            NpcSkillLevel.Novice,
            Dice(1, 1, 1, 2, 1, 3, 1, 4)
        );

        var ai = NpcGenerator.Archetypes.Single(a => a.D66 == "36");
        var specialistAcquisitions = NpcGenerator.GenerateSkills(
            ai,
            NpcSkillLevel.Novice,
            Dice(3, 4, 3, 4, 4, 4, 1, 1)
        );
        Check(
            specialistAcquisitions.Single(s => s.Name == "Electronics (Computers)").Level == 1
                && specialistAcquisitions.Single(s => s.Name == "Electronics (Sensors)").Level == 0,
            "Acquisitions increment only the exact speciality"
        );
        var specialistSheet = NpcGenerator.RecordedSkills(specialistAcquisitions);
        var cappedSpecialities = NpcGenerator.GenerateSkills(
            ai,
            NpcSkillLevel.Veteran,
            Dice(
                Enumerable
                    .Repeat(new[] { 3, 4 }, 7)
                    .SelectMany(pair => pair)
                    .Concat(Enumerable.Repeat(new[] { 4, 4 }, 4).SelectMany(pair => pair))
                    .ToArray()
            )
        );
        Check(
            cappedSpecialities.Single(s => s.Name == "Electronics (Computers)").Level == 5
                && cappedSpecialities.Single(s => s.Name == "Electronics (Sensors)").Level == 3,
            "Rank caps apply separately to each speciality, not the shared base skill"
        );
        Check(
            specialistSheet.Single(s => s.Name == "Electronics").Level == 0
                && specialistSheet.Single(s => s.Name == "Electronics (Computers)").Level == 1
                && specialistSheet.All(s => s.Name != "Electronics (Sensors)"),
            "Recorded skills include base rank zero and omit rank-zero specialities"
        );
        var both = NpcGenerator.RecordedSkills(
            NpcGenerator.GenerateSkills(ai, NpcSkillLevel.Novice, Dice(3, 4, 3, 4, 4, 4, 4, 4))
        );
        Check(
            both.Count(s => s.Name == "Electronics") == 1
                && both.Single(s => s.Name == "Electronics (Computers)").Level == 1
                && both.Single(s => s.Name == "Electronics (Sensors)").Level == 1,
            "Different trained specialities share one base entry without merging ranks"
        );
        var combatant = NpcGenerator.Generate(
            1,
            d6: Dice(
                new[] { 4, 6, 3, 4, 3, 4, 4, 4, 4, 4, 6 }.Concat(Enumerable.Repeat(1, 32)).ToArray()
            ),
            type: NpcType.Combatant
        );
        Check(
            combatant.Type == NpcType.Combatant
                && combatant.Occupation == ""
                && combatant.Character.Race == "Aslan"
                && combatant.Age == 27,
            "Combatants skip occupation rolls and start without a profession"
        );
        var combatPool = NpcGenerator.CombatantArchetype.Skills;
        Check(
            combatPool[5] == "Gun Combat (Slug)"
                && Array.IndexOf(combatPool.ToArray(), "Leadership") < 3
                && Array.IndexOf(combatPool.ToArray(), "Tactics (Military)") < 3,
            "Combatant core skill sits at seven and command skills sit outside 5-9"
        );
        var filterNpc = combatant with
        {
            Character = combatant.Character with
            {
                Name = "Mira",
                Race = "Vargr",
                Skills = [new(0, 0, "Admin", 3)],
            },
            Occupation = "Scout",
            SkillLevel = NpcSkillLevel.Expert,
        };
        foreach (
            var query in new[]
            {
                " mira ",
                "SCOUT",
                "vargr",
                "Expert",
                "6",
                "Admin",
                "Admin 3",
                "Combatant",
                "",
            }
        )
            Check(
                NpcFilter.Matches(filterNpc, query),
                "Filter matches requested fields, ranks, labels and case-insensitive text"
            );
        Check(!NpcFilter.Matches(filterNpc, "no matching NPC"), "Filter excludes unrelated text");

        var ascendingStats = new[] { 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6 };
        Check(
            NpcGenerator.GenerateUpp(
                NpcType.AllyEnemy,
                "Human",
                NpcSkillLevel.Recruit,
                Dice(ascendingStats)
            ) == "3468AC",
            "Allies retain rolled attribute order without a type bias"
        );
        Check(
            NpcGenerator.GenerateUpp(
                NpcType.PatronCivilian,
                "Human",
                NpcSkillLevel.Recruit,
                Dice(ascendingStats.Concat(new[] { 1, 1, 1, 1 }).ToArray())
            ) == "463AC8",
            "Patrons randomly allocate high rolls mentally and low rolls physically"
        );
        Check(
            NpcGenerator.GenerateUpp(
                NpcType.Combatant,
                "Human",
                NpcSkillLevel.Recruit,
                Dice(ascendingStats.Concat(new[] { 1, 1, 1, 1 }).ToArray())
            ) == "AC8463",
            "Combatants randomly allocate high rolls physically and low rolls mentally"
        );
        var sixes = Enumerable.Repeat(3, 12).ToArray();
        Check(
            NpcGenerator.GenerateUpp(NpcType.AllyEnemy, "Aslan", NpcSkillLevel.Recruit, Dice(sixes))
                == "846666",
            "Aslan racial modifiers apply after allocation"
        );
        Check(
            NpcGenerator.GenerateUpp(NpcType.AllyEnemy, "Vargr", NpcSkillLevel.Recruit, Dice(sixes))
                == "475666",
            "Updated Vargr racial modifiers apply after allocation"
        );
        Check(
            NpcGenerator.GenerateUpp(
                NpcType.AllyEnemy,
                "Human",
                NpcSkillLevel.Practitioner,
                Dice(sixes.Concat(new[] { 1, 6 }).ToArray())
            ) == "766667",
            "Levels two and four each add an attribute point"
        );
        Check(
            NpcGenerator.GenerateUpp(
                NpcType.AllyEnemy,
                "Human",
                NpcSkillLevel.Champion,
                Dice(sixes.Concat(new[] { 1, 2, 3, 4, 1 }).ToArray())
            ) == "677766",
            "Level nine keeps four bonuses and applies one penalty"
        );
        Check(
            NpcGenerator.GenerateUpp(
                NpcType.AllyEnemy,
                "Human",
                NpcSkillLevel.Legendary,
                Dice(sixes.Concat(Enumerable.Repeat(1, 8)).ToArray())
            ) == "666666",
            "Level twelve retains four bonuses and applies four penalties"
        );
        Check(
            NpcGenerator
                .GenerateUpp(NpcType.AllyEnemy, "Aslan", NpcSkillLevel.Elite, () => 6)
                .All(c => c is >= '3' and <= '9' or >= 'A' and <= 'C'),
            "Racial and level increases never exceed twelve"
        );
        Check(
            NpcGenerator
                .GenerateUpp(NpcType.Combatant, "Vargr", NpcSkillLevel.Legendary, () => 1)
                .All(c => c is >= '3' and <= '9' or >= 'A' and <= 'C'),
            "Low rolls, racial penalties and ageing never reduce attributes below three"
        );
        foreach (
            var (raceName, male, female) in new[]
            {
                ("Human", "Adrian Adler", "Amara Adler"),
                ("Aslan", "Aehro Ahroakh", "Aihya Ahroakh"),
                ("Vargr", "Aekhaz Akhaez", "Aezha Akhaez"),
            }
        )
        {
            Check(
                NpcGenerator.GenerateName(raceName, "Male", () => 1) == male
                    && NpcGenerator.GenerateName(raceName, "Female", () => 1) == female,
                "Name pools follow the generated race and gender"
            );
            var alternatives = Enumerable
                .Range(1, 6)
                .SelectMany(a =>
                    Enumerable
                        .Range(1, 6)
                        .Select(b => NpcGenerator.GenerateName(raceName, "Male", Dice(a, b, a, b)))
                )
                .Distinct()
                .ToArray();
            Check(
                alternatives.Length == 12
                    && alternatives.All(n => n.Length <= 120 && n.Contains(' ')),
                "Name rolls offer valid varied full names"
            );
        }
        Check(
            NpcGenerator.Quirks.Count == 36 && NpcGenerator.Quirks.Distinct().Count() == 36,
            "Quirk table has all 36 distinct supplied entries"
        );
        for (var first = 0; first < 36; first++)
        for (var second = 0; second < 36; second++)
        {
            var quirks = NpcGenerator.GenerateQuirks(
                Dice(first / 6 + 1, first % 6 + 1, second / 6 + 1, second % 6 + 1)
            );
            Check(
                quirks.First == NpcGenerator.Quirks[first]
                    && quirks.Second == (first == second ? "" : NpcGenerator.Quirks[second]),
                "Two D66 rolls map correctly, and duplicates leave the second quirk blank without rerolling"
            );
        }
        Check(
            NpcGenerator.GenerateQuirks(Dice(1, 1, 6, 6))
                == ("Loyal", "Possesses telepathy or other unusual quality"),
            "Quirk table endpoints match the supplied image"
        );
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(connection).Options;
        await using var db = new MtuDbContext(options);
        await db.Database.MigrateAsync();
        db.Campaigns.AddRange(
            new CampaignEntity { Id = 1, Name = "NPC generation" },
            new CampaignEntity { Id = 2, Name = "Other campaign" }
        );
        await db.SaveChangesAsync();
        var store = new NpcService(db, new CharacterService(db));
        var draft = NpcGenerator.Generate(1, d6: GenerationDice(1, 1, 4, 6));
        Check(!await db.Characters.AnyAsync(), "Generating and cancelling a draft writes nothing");
        draft = draft with { Character = draft.Character with { Name = "Generated officer" } };
        await store.SaveAsync(draft);
        db.ChangeTracker.Clear();
        var saved = (await store.ListAsync(1)).Single();
        Check(
            saved.Occupation == "Naval Officer"
                && saved.Gender == "Female"
                && saved.Character.Race == "Aslan"
                && saved.SkillLevel == NpcSkillLevel.Novice
                && saved.Age == 27
                && saved.Quirks1 == draft.Quirks1
                && saved.Quirks2 == draft.Quirks2
                && saved.Character.IsNpc
                && saved.Character.Upp == draft.Character.Upp,
            "Generated profile persists"
        );
        Check(
            saved
                .Character.Skills!.Select(s => (s.Name, s.Level))
                .OrderBy(s => s.Name)
                .SequenceEqual(
                    NpcGenerator
                        .RecordedSkills(biased)
                        .Select(s => (s.Name, s.Level))
                        .OrderBy(s => s.Name)
                )
                && saved.Character.Skills!.All(s =>
                    s.CharacterId == saved.Character.Id && s.Id > 0
                ),
            "Initial skills persist with the new character ID"
        );
        Check(
            !(await store.ListAsync(2)).Any()
                && !(await new CharacterService(db).ListAsync(1)).Any(),
            "Generated NPC is isolated from other campaigns and the player list"
        );
        await store.SaveAsync(
            saved with
            {
                Occupation = "Retired officer",
                Character = saved.Character with { Skills = [] },
            }
        );
        Check(
            (await store.ListAsync(1)).Single().Character.Skills!.Count == 4,
            "Editing profile leaves existing skills intact"
        );
        Check(
            (await store.ListAsync(1)).Single().Age == 27,
            "Profile edits preserve generated age"
        );
        var invalidAge = draft with { Age = -1 };
        try
        {
            await store.SaveAsync(invalidAge);
            throw new Exception("Negative age accepted");
        }
        catch (ArgumentException) { }
        var duplicate = draft with
        {
            Character = draft.Character with
            {
                Skills = [new(0, 0, "Admin", 0), new(0, 0, " admin ", 1)],
            },
        };
        try
        {
            await store.SaveAsync(duplicate);
            throw new Exception("Duplicate initial skills accepted");
        }
        catch (ArgumentException) { }
        var invalid = draft with
        {
            Character = draft.Character with { Skills = [new(0, 0, "Admin", -1)] },
        };
        try
        {
            await store.SaveAsync(invalid);
            throw new Exception("Negative skill accepted");
        }
        catch (ArgumentException) { }
        Check(
            await db.Characters.CountAsync() == 1 && await db.CharacterSkills.CountAsync() == 4,
            "Validation failure leaves no partial NPC or skills"
        );
        await store.DeleteAsync(1, saved.Character.Id);
        Check(!await db.CharacterSkills.AnyAsync(), "Generated skills cascade with NPC deletion");
        var legendary = NpcGenerator.Generate(
            1,
            NpcSkillLevel.Legendary,
            GenerationDice(1, 1, 1, 1, NpcSkillLevel.Legendary)
        );
        await store.SaveAsync(
            legendary with
            {
                Character = legendary.Character with { Name = "Legendary NPC" },
            }
        );
        db.ChangeTracker.Clear();
        var savedLegendary = (await store.ListAsync(1)).Single();
        Check(
            savedLegendary.SkillLevel == NpcSkillLevel.Legendary
                && savedLegendary.Age == 67
                && savedLegendary
                    .Character.Skills!.Select(s => (s.Name, s.Level))
                    .OrderBy(s => s.Name)
                    .SequenceEqual(
                        NpcGenerator
                            .RecordedSkills(legendary.Character.Skills!)
                            .Select(s => (s.Name, s.Level))
                            .OrderBy(s => s.Name)
                    ),
            "Level 12, age and all generated skill ranks persist"
        );
        await store.SaveAsync(
            draft with
            {
                Character = draft.Character with
                {
                    Name = "Specialist NPC",
                    Skills = specialistAcquisitions,
                },
            }
        );
        db.ChangeTracker.Clear();
        var savedSpecialist = (await store.ListAsync(1)).Single(n =>
            n.Character.Name == "Specialist NPC"
        );
        Check(
            savedSpecialist
                .Character.Skills!.Select(s => (s.Name, s.Level))
                .OrderBy(s => s.Name)
                .SequenceEqual(specialistSheet.Select(s => (s.Name, s.Level)).OrderBy(s => s.Name)),
            "Speciality recording rules survive database persistence"
        );
        await store.SaveAsync(
            combatant with
            {
                Character = combatant.Character with { Name = "Combatant test" },
            }
        );
        var savedCombatant = (await store.ListAsync(1)).Single(n =>
            n.Character.Name == "Combatant test"
        );
        Check(
            savedCombatant.Type == NpcType.Combatant && savedCombatant.Occupation == "",
            "Combatant type and blank occupation persist"
        );
        await store.SaveAsync(
            savedCombatant with
            {
                Type = NpcType.PatronCivilian,
                Occupation = "Scout",
            }
        );
        db.ChangeTracker.Clear();
        Check(
            (await store.ListAsync(1)).Single(n => n.Character.Name == "Combatant test").Type
                == NpcType.PatronCivilian,
            "NPC type can be edited without replacing skills"
        );
        Check(saved.Type == NpcType.AllyEnemy, "Existing generation remains Ally/Enemy by default");
        Check(
            NpcRaces.All.SequenceEqual(
                new[]
                {
                    "Human",
                    "Aslan",
                    "Vargr",
                    "Droyne",
                    "Bwap",
                    "K'Kree",
                    "Hiver",
                    "Dolphin",
                    "Gorilla",
                    "Robot",
                }
            ),
            "Race choices match the requested list"
        );
        foreach (var raceChoice in NpcRaces.All)
            Check(
                NpcRaces.SupportsGeneration(raceChoice)
                    == new[] { "Human", "Aslan", "Vargr" }.Contains(raceChoice),
                "Only Human, Aslan and Vargr permit automatic generation"
            );
        try
        {
            NpcGenerator.GenerateName("Robot", "");
            throw new Exception("Manual race generated a name");
        }
        catch (ArgumentException) { }
        try
        {
            NpcGenerator.GenerateUpp(NpcType.Combatant, "Droyne", NpcSkillLevel.Recruit);
            throw new Exception("Manual race generated a UPP");
        }
        catch (ArgumentException) { }
        var manual = new Npc(
            new Character(
                0,
                1,
                "",
                "Manual robot",
                "Robot",
                "",
                "0",
                0,
                0,
                0,
                Skills: [],
                IsNpc: true
            )
        );
        await store.SaveAsync(manual);
        var manualSaved = (await store.ListAsync(1)).Single(n =>
            n.Character.Name == "Manual robot"
        );
        Check(
            manualSaved.Character.Upp == ""
                && manualSaved.Character.Skills!.Count == 0
                && manualSaved.Gender == ""
                && manualSaved.Age is null
                && manualSaved.Occupation == "",
            "Manual race details remain blank through persistence"
        );
        foreach (var type in Enum.GetValues<NpcType>())
        foreach (var profession in NpcGenerator.Occupations(type))
        {
            var pool = NpcGenerator.ArchetypeFor(type, profession);
            Check(
                pool.Skills.Count == 11
                    && (type == NpcType.Combatant || pool.Occupation == profession),
                "Each type exposes valid occupations with a matching eleven-skill pool"
            );
        }
        var replacement = savedSpecialist with
        {
            Occupation = "Medical Doctor",
            Type = NpcType.AllyEnemy,
            Character = savedSpecialist.Character with
            {
                Skills = [new(0, 0, "Medic", 2), new(0, 0, "Admin", 0)],
            },
        };
        await store.SaveAsync(replacement, replaceSkills: true);
        db.ChangeTracker.Clear();
        var rerolled = (await store.ListAsync(1)).Single(n =>
            n.Character.Id == savedSpecialist.Character.Id
        );
        Check(
            rerolled.Occupation == "Medical Doctor"
                && rerolled.Character.Skills!.Count == 2
                && rerolled.Character.Skills!.Single(s => s.Name == "Medic").Level == 2
                && rerolled.Character.Skills!.All(s => !s.Name.StartsWith("Electronics")),
            "Explicit profession rerolls replace the saved skills atomically"
        );
        await store.SaveAsync(
            rerolled with
            {
                Character = rerolled.Character with
                {
                    Skills = [new(0, 0, "Medic", 3), new(0, 0, "Admin", 1)],
                },
            },
            replaceSkills: true
        );
        Check(
            (await store.ListAsync(1))
                .Single(n => n.Character.Id == rerolled.Character.Id)
                .Character.Skills!.Single(s => s.Name == "Medic")
                .Level == 3,
            "Repeated rerolls update matching skills without duplicate rows"
        );
        var worn = new EquipmentItem
        {
            Name = "Worn armour",
            Kind = EquipmentKind.Armour,
            Protection = 4,
        };
        var carried = new EquipmentItem
        {
            Name = "Carried armour",
            Kind = EquipmentKind.Armour,
            Protection = 2,
        };
        var player = new CharacterEntity { CampaignId = 1, Name = "Equipment player" };
        db.Equipment.AddRange(worn, carried);
        db.Characters.Add(player);
        await db.SaveChangesAsync();
        foreach (var id in new[] { player.Id, savedSpecialist.Character.Id })
            db.CharacterEquipment.AddRange(
                new CharacterEquipmentEntity
                {
                    CharacterId = id,
                    EquipmentId = worn.Id,
                    Equipped = true,
                },
                new CharacterEquipmentEntity
                {
                    CharacterId = id,
                    EquipmentId = carried.Id,
                    Equipped = false,
                }
            );
        await db.SaveChangesAsync();
        var characterStore = new CharacterService(db);
        Check(
            (await characterStore.ListAsync(1, equippedOnly: true)).Single().Equipment!.Single().Id
                == worn.Id,
            "Traveller table summary excludes equipment not worn or equipped"
        );
        Check(
            (await store.ListAsync(1, equippedOnly: true))
                .Single(n => n.Character.Id == savedSpecialist.Character.Id)
                .Character.Equipment!.Single()
                .Id == worn.Id,
            "NPC table summary excludes equipment not worn or equipped"
        );
        Check(
            (await new EquipmentService(db).ListDetailsForCharacterAsync(1, player.Id)).Count == 2,
            "Equipment editor retains all carried and equipped items"
        );
        await new EquipmentService(db).SetEquippedAsync(1, player.Id, worn.Id, false);
        Check(
            (await characterStore.ListAsync(1, equippedOnly: true)).Single().Equipment!.Count == 0,
            "Summary clears after the last item is unequipped"
        );
        var cloth = new EquipmentItem
        {
            Id = 101,
            Kind = EquipmentKind.Armour,
            Name = "Cloth",
            Tl = 8,
            Protection = 8,
        };
        var restricted = new EquipmentItem
        {
            Id = 102,
            Kind = EquipmentKind.Armour,
            Name = "Combat armour",
            Tl = 10,
            Protection = 20,
            RequiredSkill = "Vacc Suit 1",
        };
        var rifle = new EquipmentItem
        {
            Id = 103,
            Kind = EquipmentKind.Weapon,
            Name = "Assault rifle",
            Range = 200,
            Tl = 7,
        };
        var laser = new EquipmentItem
        {
            Id = 104,
            Kind = EquipmentKind.Weapon,
            Name = "Laser rifle",
            Range = 400,
            Tl = 10,
        };
        var knife = new EquipmentItem
        {
            Id = 105,
            Kind = EquipmentKind.Weapon,
            Name = "Combat knife",
            Range = 0,
            Tl = 2,
        };
        var infantry = combatant with
        {
            Character = combatant.Character with { Skills = [new(0, 0, "Gun Combat (Slug)", 2)] },
        };
        var chosen = CombatantLoadouts.Select(infantry, [cloth, restricted, rifle, laser, knife]);
        Check(
            chosen.Select(e => e.Id).SequenceEqual(new[] { 101, 103, 105 }),
            "Combatants receive usable armour, a weapon matching their skill, and a melee backup"
        );
        Check(
            CombatantLoadouts.Select(infantry, [restricted]).Count == 0
                && CombatantLoadouts.Select(infantry, []).Count == 0
                && CombatantLoadouts
                    .Select(infantry with { Type = NpcType.AllyEnemy }, [cloth, rifle])
                    .Count == 0,
            "Restricted armour, empty catalogues and other NPC types do not receive inappropriate items"
        );
        // Use real catalogue rows and verify the reviewed draft loadout persists unchanged.
        var autoNpc = infantry with
        {
            Character = infantry.Character with
            {
                Name = "Auto-equipped NPC",
                Equipment = [worn, carried],
            },
        };
        await store.SaveAsync(autoNpc);
        var autoSaved = (await store.ListAsync(1)).Single(n =>
            n.Character.Name == "Auto-equipped NPC"
        );
        var autoDetails = await new EquipmentService(db).ListDetailsForCharacterAsync(
            1,
            autoSaved.Character.Id
        );
        Check(
            autoDetails.Count == 2 && autoDetails.All(e => e.Equipped && e.Quantity == 1),
            "New Combatant assignments are automatically worn/equipped with quantity one"
        );
        await new EquipmentService(db).SetEquippedAsync(1, autoSaved.Character.Id, worn.Id, false);
        await store.SaveAsync(autoSaved);
        Check(
            !(
                await new EquipmentService(db).ListDetailsForCharacterAsync(
                    1,
                    autoSaved.Character.Id
                )
            )
                .Single(e => e.Equipment.Id == worn.Id)
                .Equipped,
            "Editing saved Combatants never resets their equipment"
        );
        var fallbackNpc = infantry with
        {
            Character = infantry.Character with
            {
                Name = "Automatic catalogue NPC",
                Equipment = null,
            },
        };
        await store.SaveAsync(fallbackNpc);
        var fallbackSaved = (await store.ListAsync(1)).Single(n =>
            n.Character.Name == "Automatic catalogue NPC"
        );
        Check(
            (
                await new EquipmentService(db).ListDetailsForCharacterAsync(
                    1,
                    fallbackSaved.Character.Id
                )
            ).Any(e => e.Equipped),
            "Combatant creation outside the UI also selects a catalogue loadout automatically"
        );
        Console.WriteLine(
            "NPC generation dice, skill ranks, persistence and isolation checks passed."
        );
    }
}
