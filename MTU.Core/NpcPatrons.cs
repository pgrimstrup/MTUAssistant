namespace MTU.Core;

public static partial class NpcGenerator
{
    private static IReadOnlyList<string> AllySkills(string occupation) =>
        Archetypes.Single(a => a.Occupation == occupation).Skills;

    public static IReadOnlyList<NpcArchetype> PatronArchetypes => patronArchetypes.Value;
    private static readonly Lazy<IReadOnlyList<NpcArchetype>> patronArchetypes = new(() =>
        Array.AsReadOnly<NpcArchetype>([
            new(
                "11",
                "Assassin",
                [
                    "Medic",
                    "Survival",
                    "Athletics (Dexterity)",
                    "Electronics (Computers)",
                    "Gun Combat (Slug)",
                    "Stealth",
                    "Recon",
                    "Melee (Blade)",
                    "Deception",
                    "Streetwise",
                    "Explosives",
                ]
            ),
            new("12", "Smuggler", AllySkills("Smuggler")),
            new(
                "13",
                "Terrorist",
                [
                    "Admin",
                    "Survival",
                    "Gun Combat (Slug)",
                    "Deception",
                    "Stealth",
                    "Explosives",
                    "Recon",
                    "Electronics (Comms)",
                    "Heavy Weapons (Man Portable)",
                    "Streetwise",
                    "Persuade",
                ]
            ),
            new(
                "14",
                "Embezzler",
                [
                    "Carouse",
                    "Language (Vilani)",
                    "Investigate",
                    "Persuade",
                    "Admin",
                    "Deception",
                    "Broker",
                    "Electronics (Computers)",
                    "Advocate",
                    "Streetwise",
                    "Science (Economics)",
                ]
            ),
            new("15", "Thief", AllySkills("Alien Thief")),
            new(
                "16",
                "Revolutionary",
                [
                    "Medic",
                    "Admin",
                    "Gun Combat (Slug)",
                    "Deception",
                    "Persuade",
                    "Leadership",
                    "Streetwise",
                    "Stealth",
                    "Explosives",
                    "Recon",
                    "Survival",
                ]
            ),
            new(
                "21",
                "Clerk",
                [
                    "Steward",
                    "Language (Galanglic)",
                    "Broker",
                    "Persuade",
                    "Electronics (Computers)",
                    "Admin",
                    "Investigate",
                    "Advocate",
                    "Electronics (Comms)",
                    "Science (Economics)",
                    "Carouse",
                ]
            ),
            new(
                "22",
                "Administrator",
                [
                    "Language (Galanglic)",
                    "Carouse",
                    "Electronics (Comms)",
                    "Persuade",
                    "Advocate",
                    "Admin",
                    "Electronics (Computers)",
                    "Investigate",
                    "Broker",
                    "Leadership",
                    "Steward",
                ]
            ),
            new(
                "23",
                "Mayor",
                [
                    "Carouse",
                    "Science (History)",
                    "Investigate",
                    "Advocate",
                    "Persuade",
                    "Admin",
                    "Leadership",
                    "Diplomat",
                    "Broker",
                    "Electronics (Computers)",
                    "Streetwise",
                ]
            ),
            new("24", "Minor Noble", AllySkills("Rich Noble")),
            new("25", "Physician", AllySkills("Medical Doctor")),
            new(
                "26",
                "Tribal Leader",
                [
                    "Animals (Handling)",
                    "Melee (Blade)",
                    "Medic",
                    "Diplomat",
                    "Survival",
                    "Leadership",
                    "Persuade",
                    "Recon",
                    "Navigation",
                    "Science (Philosophy)",
                    "Art (Performer)",
                ]
            ),
            new("31", "Diplomat", AllySkills("Imperial Diplomat")),
            new(
                "32",
                "Courier",
                [
                    "Melee (Unarmed)",
                    "Medic",
                    "Electronics (Computers)",
                    "Athletics (Endurance)",
                    "Navigation",
                    "Pilot (Small Craft)",
                    "Drive (Wheel)",
                    "Recon",
                    "Streetwise",
                    "Stealth",
                    "Vacc Suit",
                ]
            ),
            new(
                "33",
                "Spy",
                [
                    "Language (Vilani)",
                    "Admin",
                    "Gun Combat (Slug)",
                    "Recon",
                    "Investigate",
                    "Deception",
                    "Stealth",
                    "Electronics (Computers)",
                    "Streetwise",
                    "Persuade",
                    "Melee (Unarmed)",
                ]
            ),
            new("34", "Ambassador", AllySkills("Alien Ambassador")),
            new("35", "Noble", AllySkills("Rich Noble")),
            new(
                "36",
                "Police Officer",
                [
                    "Drive (Wheel)",
                    "Admin",
                    "Athletics (Endurance)",
                    "Electronics (Comms)",
                    "Advocate",
                    "Investigate",
                    "Gun Combat (Slug)",
                    "Recon",
                    "Streetwise",
                    "Melee (Unarmed)",
                    "Medic",
                ]
            ),
            new("41", "Merchant", AllySkills("Free Trader")),
            new("42", "Free Trader", AllySkills("Free Trader")),
            new(
                "43",
                "Broker",
                [
                    "Steward",
                    "Carouse",
                    "Electronics (Computers)",
                    "Advocate",
                    "Persuade",
                    "Broker",
                    "Admin",
                    "Investigate",
                    "Deception",
                    "Science (Economics)",
                    "Streetwise",
                ]
            ),
            new("44", "Corporate Executive", AllySkills("Corporate Executive")),
            new("45", "Corporate Agent", AllySkills("Corporate Agent")),
            new(
                "46",
                "Financier",
                [
                    "Carouse",
                    "Language (Vilani)",
                    "Electronics (Computers)",
                    "Investigate",
                    "Broker",
                    "Science (Economics)",
                    "Admin",
                    "Advocate",
                    "Persuade",
                    "Deception",
                    "Leadership",
                ]
            ),
            new(
                "51",
                "Belter",
                [
                    "Medic",
                    "Survival",
                    "Pilot (Small Craft)",
                    "Electronics (Sensors)",
                    "Vacc Suit",
                    "Profession (Belter)",
                    "Mechanic",
                    "Astrogation",
                    "Explosives",
                    "Science (Planetology)",
                    "Athletics (Endurance)",
                ]
            ),
            new("52", "Researcher", AllySkills("Researcher")),
            new("53", "Naval Officer", AllySkills("Naval Officer")),
            new(
                "54",
                "Pilot",
                [
                    "Carouse",
                    "Medic",
                    "Navigation",
                    "Mechanic",
                    "Electronics (Sensors)",
                    "Pilot (Spacecraft)",
                    "Astrogation",
                    "Engineer (M-drive)",
                    "Vacc Suit",
                    "Pilot (Small Craft)",
                    "Admin",
                ]
            ),
            new("55", "Starport Administrator", AllySkills("Starport Administrator")),
            new(
                "56",
                "Scout",
                [
                    "Language (Galanglic)",
                    "Medic",
                    "Electronics (Sensors)",
                    "Survival",
                    "Recon",
                    "Pilot (Spacecraft)",
                    "Astrogation",
                    "Navigation",
                    "Science (Planetology)",
                    "Mechanic",
                    "Vacc Suit",
                ]
            ),
            new(
                "61",
                "Alien",
                [
                    "Carouse",
                    "Admin",
                    "Art (Visual Media)",
                    "Investigate",
                    "Language (Galanglic)",
                    "Science (Sophontology)",
                    "Persuade",
                    "Survival",
                    "Navigation",
                    "Medic",
                    "Electronics (Computers)",
                ]
            ),
            new(
                "62",
                "Playboy",
                [
                    "Steward",
                    "Language (Vilani)",
                    "Art (Instrument)",
                    "Gambler",
                    "Persuade",
                    "Carouse",
                    "Deception",
                    "Broker",
                    "Melee (Blade)",
                    "Pilot (Small Craft)",
                    "Athletics (Dexterity)",
                ]
            ),
            new(
                "63",
                "Stowaway",
                [
                    "Carouse",
                    "Medic",
                    "Melee (Unarmed)",
                    "Athletics (Endurance)",
                    "Streetwise",
                    "Stealth",
                    "Deception",
                    "Survival",
                    "Mechanic",
                    "Vacc Suit",
                    "Recon",
                ]
            ),
            new(
                "64",
                "Family Relative",
                [
                    "Drive (Wheel)",
                    "Electronics (Comms)",
                    "Medic",
                    "Persuade",
                    "Carouse",
                    "Admin",
                    "Steward",
                    "Profession (Construction)",
                    "Broker",
                    "Language (Galanglic)",
                    "Art (Instrument)",
                ]
            ),
            new(
                "65",
                "Agent of a Foreign Power",
                [
                    "Language (Vilani)",
                    "Admin",
                    "Gun Combat (Slug)",
                    "Diplomat",
                    "Investigate",
                    "Deception",
                    "Stealth",
                    "Electronics (Computers)",
                    "Persuade",
                    "Recon",
                    "Streetwise",
                ]
            ),
            new("66", "Imperial Agent", AllySkills("Imperial Agent")),
        ])
    );

    // 2-4 and 10-12 contain command/support skills; 5-9 favour the grunt's field skills.
    public static NpcArchetype CombatantArchetype { get; } =
        new(
            "",
            "",
            [
                "Leadership",
                "Tactics (Military)",
                "Electronics (Comms)",
                "Recon",
                "Athletics (Dexterity)",
                "Gun Combat (Slug)",
                "Melee (Blade)",
                "Gun Combat (Energy)",
                "Stealth",
                "Heavy Weapons (Man Portable)",
                "Persuade",
            ]
        );

    public static IReadOnlyList<string> Occupations(NpcType type) =>
        type switch
        {
            NpcType.AllyEnemy => Archetypes
                .Select(a => a.Occupation)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            NpcType.PatronCivilian => PatronArchetypes
                .Select(a => a.Occupation)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            NpcType.Combatant =>
            [
                "",
                "Guard",
                "Marine",
                "Mercenary",
                "Militia",
                "Scout",
                "Security Officer",
                "Sniper",
                "Soldier",
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    public static NpcArchetype ArchetypeFor(Npc npc) => ArchetypeFor(npc.Type, npc.Occupation);

    public static NpcArchetype ArchetypeFor(NpcType type, string occupation) =>
        type switch
        {
            NpcType.Combatant => CombatantArchetype,
            NpcType.PatronCivilian => PatronArchetypes.Single(a => a.Occupation == occupation),
            NpcType.AllyEnemy => Archetypes.Single(a => a.Occupation == occupation),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
}
