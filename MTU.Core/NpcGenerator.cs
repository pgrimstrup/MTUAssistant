namespace MTU.Core;

public sealed record NpcArchetype(string D66, string Occupation, IReadOnlyList<string> Skills);

public static partial class NpcGenerator
{
    // Slots correspond to 2D6 totals 2-12; core skills sit at the centre.
    public static IReadOnlyList<NpcArchetype> Archetypes { get; } =
        Array.AsReadOnly<NpcArchetype>([
            new(
                "11",
                "Naval Officer",
                [
                    "Carouse",
                    "Medic",
                    "Admin",
                    "Electronics (Sensors)",
                    "Leadership",
                    "Pilot (Spacecraft)",
                    "Gunner (Turret)",
                    "Astrogation",
                    "Tactics (Naval)",
                    "Engineer (M-drive)",
                    "Vacc Suit",
                ]
            ),
            new(
                "12",
                "Imperial Diplomat",
                [
                    "Steward",
                    "Carouse",
                    "Language (Vilani)",
                    "Investigate",
                    "Advocate",
                    "Diplomat",
                    "Persuade",
                    "Admin",
                    "Deception",
                    "Science (Sophontology)",
                    "Electronics (Computers)",
                ]
            ),
            new(
                "13",
                "Crooked Trader",
                [
                    "Mechanic",
                    "Gambler",
                    "Astrogation",
                    "Admin",
                    "Deception",
                    "Broker",
                    "Streetwise",
                    "Persuade",
                    "Pilot (Spacecraft)",
                    "Electronics (Computers)",
                    "Steward",
                ]
            ),
            new(
                "14",
                "Medical Doctor",
                [
                    "Steward",
                    "Persuade",
                    "Science (Chemistry)",
                    "Electronics (Computers)",
                    "Science (Biology)",
                    "Medic",
                    "Investigate",
                    "Science (Genetics)",
                    "Admin",
                    "Science (Psychology)",
                    "Athletics (Endurance)",
                ]
            ),
            new(
                "15",
                "Eccentric Scientist",
                [
                    "Carouse",
                    "Mechanic",
                    "Admin",
                    "Engineer (Power)",
                    "Investigate",
                    "Science (Physics)",
                    "Science (Astronomy)",
                    "Electronics (Computers)",
                    "Science (Chemistry)",
                    "Science (Robotics)",
                    "Vacc Suit",
                ]
            ),
            new(
                "16",
                "Mercenary",
                [
                    "Drive (Wheel)",
                    "Medic",
                    "Vacc Suit",
                    "Athletics (Dexterity)",
                    "Tactics (Military)",
                    "Gun Combat (Slug)",
                    "Heavy Weapons (Man Portable)",
                    "Melee (Blade)",
                    "Recon",
                    "Stealth",
                    "Survival",
                ]
            ),
            new(
                "21",
                "Famous Performer",
                [
                    "Language (Galanglic)",
                    "Electronics (Computers)",
                    "Steward",
                    "Admin",
                    "Carouse",
                    "Art (Performer)",
                    "Persuade",
                    "Deception",
                    "Art (Holography)",
                    "Athletics (Dexterity)",
                    "Gambler",
                ]
            ),
            new(
                "22",
                "Alien Thief",
                [
                    "Carouse",
                    "Survival",
                    "Melee (Unarmed)",
                    "Athletics (Dexterity)",
                    "Deception",
                    "Stealth",
                    "Streetwise",
                    "Electronics (Computers)",
                    "Recon",
                    "Investigate",
                    "Language (Galanglic)",
                ]
            ),
            new(
                "23",
                "Free Trader",
                [
                    "Steward",
                    "Vacc Suit",
                    "Electronics (Sensors)",
                    "Mechanic",
                    "Broker",
                    "Pilot (Spacecraft)",
                    "Astrogation",
                    "Admin",
                    "Engineer (Power)",
                    "Persuade",
                    "Carouse",
                ]
            ),
            new(
                "24",
                "Explorer",
                [
                    "Medic",
                    "Mechanic",
                    "Vacc Suit",
                    "Athletics (Endurance)",
                    "Navigation",
                    "Survival",
                    "Recon",
                    "Pilot (Small Craft)",
                    "Science (Planetology)",
                    "Science (Xenology)",
                    "Electronics (Sensors)",
                ]
            ),
            new(
                "25",
                "Marine Captain",
                [
                    "Medic",
                    "Admin",
                    "Survival",
                    "Vacc Suit",
                    "Tactics (Military)",
                    "Leadership",
                    "Gun Combat (Slug)",
                    "Melee (Blade)",
                    "Athletics (Strength)",
                    "Recon",
                    "Heavy Weapons (Man Portable)",
                ]
            ),
            new(
                "26",
                "Corporate Executive",
                [
                    "Carouse",
                    "Language (Vilani)",
                    "Investigate",
                    "Deception",
                    "Broker",
                    "Admin",
                    "Leadership",
                    "Advocate",
                    "Persuade",
                    "Diplomat",
                    "Electronics (Computers)",
                ]
            ),
            new(
                "31",
                "Researcher",
                [
                    "Language (Galanglic)",
                    "Persuade",
                    "Science (Biology)",
                    "Science (Chemistry)",
                    "Science (Physics)",
                    "Investigate",
                    "Electronics (Computers)",
                    "Admin",
                    "Science (Sophontology)",
                    "Science (Psychology)",
                    "Mechanic",
                ]
            ),
            new(
                "32",
                "Cultural Attaché",
                [
                    "Steward",
                    "Electronics (Computers)",
                    "Language (Vilani)",
                    "Carouse",
                    "Persuade",
                    "Diplomat",
                    "Science (Sophontology)",
                    "Admin",
                    "Art (Visual Media)",
                    "Investigate",
                    "Science (History)",
                ]
            ),
            new(
                "33",
                "Religious Leader",
                [
                    "Steward",
                    "Language (Vilani)",
                    "Art (Visual Media)",
                    "Admin",
                    "Persuade",
                    "Leadership",
                    "Diplomat",
                    "Science (Philosophy)",
                    "Investigate",
                    "Carouse",
                    "Medic",
                ]
            ),
            new(
                "34",
                "Conspirator",
                [
                    "Gambler",
                    "Admin",
                    "Electronics (Computers)",
                    "Investigate",
                    "Stealth",
                    "Deception",
                    "Streetwise",
                    "Persuade",
                    "Recon",
                    "Diplomat",
                    "Melee (Unarmed)",
                ]
            ),
            new(
                "35",
                "Rich Noble",
                [
                    "Steward",
                    "Language (Vilani)",
                    "Art (Instrument)",
                    "Diplomat",
                    "Carouse",
                    "Admin",
                    "Advocate",
                    "Broker",
                    "Persuade",
                    "Leadership",
                    "Melee (Blade)",
                ]
            ),
            new(
                "36",
                "Artificial Intelligence",
                [
                    "Language (Galanglic)",
                    "Science (Physics)",
                    "Engineer (Power)",
                    "Astrogation",
                    "Investigate",
                    "Electronics (Computers)",
                    "Electronics (Sensors)",
                    "Admin",
                    "Science (Robotics)",
                    "Science (Cybernetics)",
                    "Science (Cosmology)",
                ]
            ),
            new(
                "41",
                "Bored Noble",
                [
                    "Steward",
                    "Language (Vilani)",
                    "Art (Instrument)",
                    "Broker",
                    "Gambler",
                    "Carouse",
                    "Persuade",
                    "Admin",
                    "Melee (Blade)",
                    "Deception",
                    "Pilot (Small Craft)",
                ]
            ),
            new(
                "42",
                "Planetary Governor",
                [
                    "Carouse",
                    "Electronics (Computers)",
                    "Investigate",
                    "Persuade",
                    "Leadership",
                    "Admin",
                    "Advocate",
                    "Diplomat",
                    "Broker",
                    "Science (Economics)",
                    "Science (History)",
                ]
            ),
            new(
                "43",
                "Inveterate Gambler",
                [
                    "Melee (Blade)",
                    "Electronics (Computers)",
                    "Stealth",
                    "Persuade",
                    "Deception",
                    "Gambler",
                    "Carouse",
                    "Streetwise",
                    "Investigate",
                    "Broker",
                    "Admin",
                ]
            ),
            new(
                "44",
                "Crusading Journalist",
                [
                    "Carouse",
                    "Language (Galanglic)",
                    "Admin",
                    "Streetwise",
                    "Persuade",
                    "Investigate",
                    "Electronics (Computers)",
                    "Deception",
                    "Art (Write)",
                    "Advocate",
                    "Stealth",
                ]
            ),
            new(
                "45",
                "Doomsday Cultist",
                [
                    "Medic",
                    "Admin",
                    "Melee (Blade)",
                    "Survival",
                    "Leadership",
                    "Persuade",
                    "Stealth",
                    "Explosives",
                    "Deception",
                    "Recon",
                    "Gun Combat (Slug)",
                ]
            ),
            new(
                "46",
                "Corporate Agent",
                [
                    "Carouse",
                    "Admin",
                    "Advocate",
                    "Streetwise",
                    "Deception",
                    "Investigate",
                    "Electronics (Computers)",
                    "Gun Combat (Slug)",
                    "Stealth",
                    "Persuade",
                    "Broker",
                ]
            ),
            new(
                "51",
                "Criminal Syndicate",
                [
                    "Carouse",
                    "Admin",
                    "Melee (Unarmed)",
                    "Gun Combat (Slug)",
                    "Deception",
                    "Streetwise",
                    "Broker",
                    "Leadership",
                    "Persuade",
                    "Stealth",
                    "Gambler",
                ]
            ),
            new(
                "52",
                "Military Governor",
                [
                    "Carouse",
                    "Electronics (Comms)",
                    "Gun Combat (Slug)",
                    "Diplomat",
                    "Tactics (Military)",
                    "Leadership",
                    "Admin",
                    "Advocate",
                    "Persuade",
                    "Recon",
                    "Broker",
                ]
            ),
            new(
                "53",
                "Army Quartermaster",
                [
                    "Drive (Wheel)",
                    "Medic",
                    "Heavy Weapons (Man Portable)",
                    "Gun Combat (Slug)",
                    "Broker",
                    "Admin",
                    "Mechanic",
                    "Electronics (Comms)",
                    "Advocate",
                    "Persuade",
                    "Vacc Suit",
                ]
            ),
            new(
                "54",
                "Private Investigator",
                [
                    "Drive (Wheel)",
                    "Admin",
                    "Advocate",
                    "Stealth",
                    "Deception",
                    "Investigate",
                    "Streetwise",
                    "Gun Combat (Slug)",
                    "Electronics (Computers)",
                    "Persuade",
                    "Recon",
                ]
            ),
            new(
                "55",
                "Starport Administrator",
                [
                    "Steward",
                    "Mechanic",
                    "Pilot (Spacecraft)",
                    "Diplomat",
                    "Broker",
                    "Admin",
                    "Electronics (Sensors)",
                    "Investigate",
                    "Advocate",
                    "Leadership",
                    "Vacc Suit",
                ]
            ),
            new(
                "56",
                "Retired Admiral",
                [
                    "Carouse",
                    "Art (Visual Media)",
                    "Pilot (Spacecraft)",
                    "Astrogation",
                    "Tactics (Naval)",
                    "Leadership",
                    "Admin",
                    "Diplomat",
                    "Gunner (Turret)",
                    "Advocate",
                    "Science (History)",
                ]
            ),
            new(
                "61",
                "Alien Ambassador",
                [
                    "Steward",
                    "Carouse",
                    "Science (Sophontology)",
                    "Investigate",
                    "Persuade",
                    "Diplomat",
                    "Language (Galanglic)",
                    "Admin",
                    "Advocate",
                    "Deception",
                    "Electronics (Computers)",
                ]
            ),
            new(
                "62",
                "Smuggler",
                [
                    "Mechanic",
                    "Vacc Suit",
                    "Gun Combat (Slug)",
                    "Electronics (Sensors)",
                    "Streetwise",
                    "Pilot (Spacecraft)",
                    "Deception",
                    "Astrogation",
                    "Broker",
                    "Stealth",
                    "Persuade",
                ]
            ),
            new(
                "63",
                "Weapons Inspector",
                [
                    "Mechanic",
                    "Vacc Suit",
                    "Tactics (Military)",
                    "Admin",
                    "Advocate",
                    "Investigate",
                    "Gun Combat (Slug)",
                    "Electronics (Computers)",
                    "Heavy Weapons (Man Portable)",
                    "Science (Physics)",
                    "Explosives",
                ]
            ),
            new(
                "64",
                "Elder Statesman",
                [
                    "Art (Instrument)",
                    "Language (Vilani)",
                    "Investigate",
                    "Persuade",
                    "Leadership",
                    "Diplomat",
                    "Admin",
                    "Advocate",
                    "Science (History)",
                    "Broker",
                    "Carouse",
                ]
            ),
            new(
                "65",
                "Planetary Warlord",
                [
                    "Survival",
                    "Carouse",
                    "Admin",
                    "Heavy Weapons (Artillery)",
                    "Tactics (Military)",
                    "Leadership",
                    "Gun Combat (Slug)",
                    "Melee (Blade)",
                    "Persuade",
                    "Recon",
                    "Drive (Wheel)",
                ]
            ),
            new(
                "66",
                "Imperial Agent",
                [
                    "Language (Vilani)",
                    "Admin",
                    "Electronics (Computers)",
                    "Stealth",
                    "Investigate",
                    "Deception",
                    "Gun Combat (Slug)",
                    "Streetwise",
                    "Persuade",
                    "Recon",
                    "Diplomat",
                ]
            ),
        ]);

    public static int SkillRollCount(NpcSkillLevel level)
    {
        if (!Enum.IsDefined(level))
            throw new ArgumentException("Select a valid skill level.");
        // The stored enum is zero-based; generation uses Recruit = 1 through Legendary = 12.
        return 2 * (1 + (int)level);
    }

    public static int GenerateAge(NpcSkillLevel level, Func<int>? d6 = null)
    {
        _ = SkillRollCount(level);
        d6 ??= () => Random.Shared.Next(1, 7);
        return 16 + 4 * (1 + (int)level) + Roll(d6) - 3;
    }

    public static Npc Generate(
        int campaignId,
        NpcSkillLevel level = NpcSkillLevel.Novice,
        Func<int>? d6 = null,
        NpcType type = NpcType.AllyEnemy
    )
    {
        if (campaignId <= 0)
            throw new ArgumentException("Select a campaign.");
        _ = SkillRollCount(level);
        d6 ??= () => Random.Shared.Next(1, 7);
        if (!Enum.IsDefined(type))
            throw new ArgumentException("Select a valid NPC type.");
        var code = type == NpcType.Combatant ? "" : $"{Roll(d6)}{Roll(d6)}";
        var archetype =
            type == NpcType.Combatant
                ? CombatantArchetype
                : (type == NpcType.PatronCivilian ? PatronArchetypes : Archetypes).Single(a =>
                    a.D66 == code
                );
        var race = Roll(d6) switch
        {
            <= 3 => "Human",
            <= 5 => "Aslan",
            _ => "Vargr",
        };
        var gender = Roll(d6) <= 3 ? "Male" : "Female";
        var skills = GenerateSkills(archetype, level, d6);
        var age = GenerateAge(level, d6);
        var upp = GenerateUpp(type, race, level, d6);
        var name = GenerateName(race, gender, d6);
        var quirks = GenerateQuirks(d6);
        return new Npc(
            new Character(
                0,
                campaignId,
                "",
                name,
                race,
                upp,
                "0",
                0,
                0,
                0,
                Skills: skills,
                IsNpc: true
            ),
            archetype.Occupation,
            gender,
            Quirks1: quirks.First,
            Quirks2: quirks.Second,
            SkillLevel: level,
            Age: age,
            Type: type
        );
    }

    public static IReadOnlyList<CharacterSkill> GenerateSkills(
        NpcArchetype archetype,
        NpcSkillLevel level,
        Func<int>? d6 = null
    )
    {
        var count = SkillRollCount(level);
        if (
            !Archetypes.Contains(archetype)
            && !PatronArchetypes.Contains(archetype)
            && archetype != CombatantArchetype
        )
            throw new ArgumentException("Select a valid NPC type.");
        d6 ??= () => Random.Shared.Next(1, 7);
        var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < count; )
        {
            var index = Roll(d6) + Roll(d6) - 2;
            var skill = archetype.Skills[index];
            if (ranks.TryGetValue(skill, out var rank) && rank >= 5)
                continue; // Six acquisitions: rank 0 followed by five increases.
            ranks[skill] = ranks.ContainsKey(skill) ? rank + 1 : 0;
            i++;
        }
        return ranks.Select(s => new CharacterSkill(0, 0, s.Key, s.Value)).ToArray();
    }

    /// <summary>Convert acquisition ranks to the character sheet without losing
    /// speciality-specific acquisition history during generation.</summary>
    public static CharacterSkill[] RecordedSkills(IEnumerable<CharacterSkill> acquisitions)
    {
        var recorded = new Dictionary<string, CharacterSkill>(StringComparer.OrdinalIgnoreCase);
        void Add(CharacterSkill skill)
        {
            if (!recorded.TryGetValue(skill.Name, out var existing) || skill.Level > existing.Level)
                recorded[skill.Name] = skill;
        }
        foreach (var skill in acquisitions)
        {
            var (definition, speciality) = CharacterSkillCatalogue.Parse(skill.Name);
            if (definition is not null && speciality is not null)
            {
                Add(new CharacterSkill(0, skill.CharacterId, definition.Name, 0));
                if (skill.Level >= 1)
                    Add(skill);
            }
            else
                Add(skill);
        }
        return recorded.Values.ToArray();
    }

    private static int Roll(Func<int> d6)
    {
        var roll = d6();
        return roll is >= 1 and <= 6
            ? roll
            : throw new ArgumentException("A D6 roll must be between 1 and 6.");
    }
}
