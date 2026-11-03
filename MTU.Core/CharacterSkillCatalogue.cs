namespace MTU.Core;

/// <summary>Core skills and example specialities for Mongoose Traveller 2e, 2022 update.
/// Language, Profession and other specialities can be extended by the referee.</summary>
public sealed record SkillDefinition(string Name, IReadOnlyList<string> Specialities);

public static class CharacterSkillCatalogue
{
    public static IReadOnlyList<SkillDefinition> All { get; } =
        Array.AsReadOnly<SkillDefinition>([
            new("Admin", []),
            new("Advocate", []),
            new("Animals", ["Handling", "Training", "Veterinary"]),
            new("Art", ["Holography", "Instrument", "Performer", "Visual Media", "Write"]),
            new("Astrogation", []),
            new("Athletics", ["Dexterity", "Endurance", "Strength"]),
            new("Broker", []),
            new("Carouse", []),
            new("Deception", []),
            new("Diplomat", []),
            new("Drive", ["Hovercraft", "Mole", "Track", "Walker", "Wheel"]),
            new("Electronics", ["Comms", "Computers", "Remote Ops", "Sensors"]),
            new("Engineer", ["J-drive", "Life Support", "M-drive", "Power"]),
            new("Explosives", []),
            new("Flyer", ["Airship", "Grav", "Ornithopter", "Rotor", "Wing"]),
            new("Gambler", []),
            new("Gun Combat", ["Archaic", "Energy", "Slug"]),
            new("Gunner", ["Capital", "Ortillery", "Screen", "Turret"]),
            new("Heavy Weapons", ["Artillery", "Man Portable", "Vehicle"]),
            new("Investigate", []),
            new("Jack-of-All-Trades", []),
            new("Language", ["Galanglic", "Gvegh", "Oynprith", "Trokh", "Vilani", "Zdetl"]),
            new("Leadership", []),
            new("Mechanic", []),
            new("Medic", []),
            new("Melee", ["Blade", "Bludgeon", "Natural", "Unarmed"]),
            new("Navigation", []),
            new("Persuade", []),
            new("Pilot", ["Capital Ships", "Small Craft", "Spacecraft"]),
            new(
                "Profession",
                [
                    "Belter",
                    "Biologicals",
                    "Civil Engineering",
                    "Construction",
                    "Hydroponics",
                    "Polymers",
                ]
            ),
            new("Recon", []),
            new(
                "Science",
                [
                    "Archaeology",
                    "Astronomy",
                    "Biology",
                    "Chemistry",
                    "Cosmology",
                    "Cybernetics",
                    "Economics",
                    "Genetics",
                    "History",
                    "Linguistics",
                    "Philosophy",
                    "Physics",
                    "Planetology",
                    "Psionicology",
                    "Psychology",
                    "Robotics",
                    "Sophontology",
                    "Xenology",
                ]
            ),
            new("Seafarer", ["Ocean Ships", "Personal", "Sail", "Submarine"]),
            new("Stealth", []),
            new("Steward", []),
            new("Streetwise", []),
            new("Survival", []),
            new("Tactics", ["Military", "Naval"]),
            new("Vacc Suit", []),
        ]);

    public static SkillDefinition? Find(string? name) =>
        All.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static (SkillDefinition? Skill, string? Speciality) Parse(string name)
    {
        var text = name.Trim();
        var whole = Find(text);
        if (whole is not null)
            return (whole, null);
        var marker = text.IndexOf(" (", StringComparison.Ordinal);
        if (marker < 1 || !text.EndsWith(')'))
            return (null, null);
        var skill = Find(text[..marker]);
        return skill is null ? (null, null) : (skill, text[(marker + 2)..^1]);
    }
}
