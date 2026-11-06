namespace MTU.Core;

public enum AnimalArchetype
{
    Killer,
    Hunter,
    Prey,
}

public sealed record AnimalSize(
    string Name,
    int MinimumHits,
    int MaximumHits,
    string Traits,
    string Damage
);

public static class AnimalGenerator
{
    public static readonly IReadOnlyList<AnimalSize> Sizes = new AnimalSize[]
    {
        new("Mouse or Rat", 1, 2, "Small (-4)", "1"),
        new("Cat or Raccoon", 3, 5, "Small (-3)", "D3"),
        new("Badger or Dog", 6, 7, "Small (-2)", "D3"),
        new("Chimpanzee or Goat", 8, 13, "Small (-1)", "1D"),
        new("Human", 14, 28, "", "1D"),
        new("Cow or Horse", 29, 35, "Large (+1)", "2D"),
        new("Shark", 36, 49, "Large (+2)", "3D"),
        new("Rhino", 50, 70, "Large (+3)", "4D"),
        new("Elephant", 71, 90, "Large (+4)", "5D"),
        new("Carnosaur", 91, 125, "Large (+5)", "6D"),
        new("Sauropod or Whale", 125, 150, "Large (+6)", "7D"),
    };

    private static readonly string[] KillerBehaviours = ["Chaser", "Hunter", "Killer", "Pouncer"];
    private static readonly string[] HunterBehaviours =
    [
        "Chaser",
        "Carrion-Eater",
        "Hunter",
        "Hijacker",
        "Intimidator",
        "Pouncer",
        "Siren",
        "Trapper",
    ];
    private static readonly string[] PreyBehaviours =
    [
        "Carrion-Eater",
        "Eater",
        "Filter",
        "Gatherer",
        "Grazer",
        "Intermittent",
        "Reducer",
    ];

    public static Animal Generate(
        int campaignId,
        AnimalArchetype archetype,
        Func<int, int>? choose = null
    )
    {
        if (!Enum.IsDefined(archetype))
            throw new ArgumentException("Choose an animal archetype.");
        choose ??= maximum => Random.Shared.Next(maximum);
        var size = Sizes[choose(6) + choose(6)];
        var hits = size.MinimumHits + choose(size.MaximumHits - size.MinimumHits + 1);
        var typeRoll = choose(4);
        var type = archetype switch
        {
            AnimalArchetype.Killer => "Carnivore",
            AnimalArchetype.Hunter => typeRoll < 2 ? "Carnivore" : "Omnivore",
            _ => typeRoll < 2 ? "Herbivore"
            : typeRoll == 2 ? "Omnivore"
            : "Scavenger",
        };
        var behaviours = archetype switch
        {
            AnimalArchetype.Killer => KillerBehaviours,
            AnimalArchetype.Hunter => HunterBehaviours,
            _ => PreyBehaviours,
        };
        var behaviour = behaviours[choose(behaviours.Length)];
        var (speed, skills) = Profile(behaviour);
        var character = new Character(
            0,
            campaignId,
            "",
            size.Name,
            "Animal",
            "",
            "0",
            0,
            0,
            0,
            Skills: skills,
            Speed: speed
        );
        return new Animal(
            character,
            hits,
            type,
            behaviour,
            size.Traits,
            [new NaturalAttack("Charge", size.Damage)],
            InitialDescription(behaviour)
        );
    }

    public static string InitialDescription(string behaviour) =>
        behaviour switch
        {
            "Carrion-Eater" =>
                "Carrion-Eater (vulture): Scavengers that wait for all other threats to disperse before beginning. Carrion-eaters have Recon.",
            "Chaser" =>
                "Chaser (wolf): Animals that kill their prey by attacking and exhausting it after a chase. Chasers have Athletics (dexterity and/or endurance).",
            "Eater" =>
                "Eater (army ant): Eaters will eat anything they encounter, including Travellers.",
            "Filter" =>
                "Filter (earthworm): Herbivores that pass their environment through their bodies are termed filters. Unlike grazers, which move to food, filters move a flow of matter through themselves and filter out the food.",
            "Gatherer" =>
                "Gatherer (raccoon, chimpanzee): Gatherers are herbivores that collect and store food. Gatherers have Stealth.",
            "Grazer" =>
                "Grazer (antelope): Grazers move from food source to food source, often in large packs. Their primary form of defence tends to be fleeing danger.",
            "Hunter" =>
                "Hunter (baboon): Opportunistic predators that stalk easy prey. Hunters have Survival.",
            "Hijacker" =>
                "Hijacker (lion): Scavengers that steal the kills of others through brute force or weight of numbers are hijackers.",
            "Intimidator" =>
                "Intimidator (coyote): Scavengers that establish their claim to food by frightening or intimidating other creatures. Intimidators have Persuade.",
            "Killer" =>
                "Killer (shark): Carnivores that possess a raw killing instinct, attacking in a frenzied manner. Killers have Melee.",
            "Intermittent" =>
                "Intermittent (elephant): Herbivores that do not devote their entire time to searching for food.",
            "Pouncer" =>
                "Pouncer (cat): Pouncers kill by stalking and ambushing their prey. Pouncers have Stealth, Recon and Athletics (dexterity or strength).",
            "Reducer" =>
                "Reducer (vermin): Reducers are scavengers that act constantly on all available food, devouring even the remains left by other scavengers.",
            "Siren" =>
                "Siren (Venus fly-trap): Sirens create a lure to attract prey. Usually, this lure will be specific to the species the siren preys on but some rare lures are universal. Sirens have Deception.",
            "Trapper" =>
                "Trapper (spider): An animal that allows its prey to enter a trap. Generally, any creature surprised by a trapper is caught in its trap.",
            _ => throw new ArgumentException("Choose a valid animal behaviour."),
        };

    private static (int Speed, IReadOnlyList<CharacterSkill> Skills) Profile(string behaviour) =>
        behaviour switch
        {
            "Carrion-Eater" => (6, [Skill("Recon", 1)]),
            "Chaser" => (
                12,
                [Skill("Athletics (Dexterity)", 1), Skill("Athletics (Endurance)", 1)]
            ),
            "Eater" => (6, []),
            "Filter" => (3, []),
            "Gatherer" => (9, [Skill("Stealth", 1)]),
            "Grazer" => (9, [Skill("Recon", 1)]),
            "Hunter" => (12, [Skill("Survival", 1)]),
            "Hijacker" => (9, [Skill("Melee (Natural)", 1)]),
            "Intimidator" => (9, [Skill("Persuade", 1)]),
            "Killer" => (12, [Skill("Melee (Natural)", 2)]),
            "Intermittent" => (6, [Skill("Recon", 1)]),
            "Pouncer" => (
                10,
                [Skill("Stealth", 1), Skill("Recon", 1), Skill("Athletics (Dexterity)", 2)]
            ),
            "Reducer" => (6, []),
            "Siren" => (1, [Skill("Stealth", 1), Skill("Deception", 1)]),
            "Trapper" => (4, [Skill("Stealth", 1), Skill("Deception", 1)]),
            _ => throw new ArgumentException("Choose a valid animal behaviour."),
        };

    private static CharacterSkill Skill(string name, int level) => new(0, 0, name, level);
}
