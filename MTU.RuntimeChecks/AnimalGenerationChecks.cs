using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MTU.Core;
using MTU.Core.Data;

public static class AnimalGenerationChecks
{
    private static void Check(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static Animal Generate(AnimalArchetype archetype, params int[] rolls)
    {
        var queue = new Queue<int>(rolls);
        var animal = AnimalGenerator.Generate(
            1,
            archetype,
            maximum =>
            {
                var value = queue.Dequeue();
                Check(
                    value >= 0 && value < maximum,
                    "Generation draw is within the required range"
                );
                return value;
            }
        );
        Check(queue.Count == 0, "Generator consumes the expected independent draws");
        return animal;
    }

    public static async Task RunAsync()
    {
        var expected = new[]
        {
            ("Mouse or Rat", 1, 2, "Small (-4)", "1"),
            ("Cat or Raccoon", 3, 5, "Small (-3)", "D3"),
            ("Badger or Dog", 6, 7, "Small (-2)", "D3"),
            ("Chimpanzee or Goat", 8, 13, "Small (-1)", "1D"),
            ("Human", 14, 28, "", "1D"),
            ("Cow or Horse", 29, 35, "Large (+1)", "2D"),
            ("Shark", 36, 49, "Large (+2)", "3D"),
            ("Rhino", 50, 70, "Large (+3)", "4D"),
            ("Elephant", 71, 90, "Large (+4)", "5D"),
            ("Carnosaur", 91, 125, "Large (+5)", "6D"),
            ("Sauropod or Whale", 125, 150, "Large (+6)", "7D"),
        };
        for (var index = 0; index < expected.Length; index++)
        {
            var row = expected[index];
            var firstDie = Math.Min(index, 5);
            foreach (var hitsRoll in new[] { 0, row.Item3 - row.Item2 })
            {
                var animal = Generate(
                    AnimalArchetype.Killer,
                    firstDie,
                    index - firstDie,
                    hitsRoll,
                    0,
                    0
                );
                Check(
                    animal.Character.Name == row.Item1
                        && animal.Hits == row.Item2 + hitsRoll
                        && animal.Traits == row.Item4
                        && animal.NaturalAttacks.Single() == new NaturalAttack("Charge", row.Item5),
                    "Size row and inclusive Hits endpoints " + index
                );
                Check(
                    AttackResolution.TryRollDamage(animal.NaturalAttacks[0].Damage, _ => 1, out _),
                    "Every generated Charge damage expression can be resolved"
                );
            }
        }
        var frequencies = new Dictionary<string, int>();
        for (var first = 0; first < 6; first++)
        for (var second = 0; second < 6; second++)
        {
            var animal = Generate(AnimalArchetype.Prey, first, second, 0, 0, 0);
            frequencies[animal.Character.Name] =
                frequencies.GetValueOrDefault(animal.Character.Name) + 1;
        }
        Check(
            expected
                .Select(e => frequencies[e.Item1])
                .SequenceEqual(new[] { 1, 2, 3, 4, 5, 6, 5, 4, 3, 2, 1 }),
            "Animal size follows a true 2D distribution"
        );
        var hits = Enumerable
            .Range(0, 15)
            .Select(roll => Generate(AnimalArchetype.Killer, 2, 2, roll, 0, 0).Hits);
        Check(
            hits.SequenceEqual(Enumerable.Range(14, 15)),
            "Hits use one uniform draw across the entire size range"
        );
        var behaviours = new Dictionary<AnimalArchetype, string[]>
        {
            [AnimalArchetype.Killer] = ["Chaser", "Hunter", "Killer", "Pouncer"],
            [AnimalArchetype.Hunter] =
            [
                "Chaser",
                "Carrion-Eater",
                "Hunter",
                "Hijacker",
                "Intimidator",
                "Pouncer",
                "Siren",
                "Trapper",
            ],
            [AnimalArchetype.Prey] =
            [
                "Carrion-Eater",
                "Eater",
                "Filter",
                "Gatherer",
                "Grazer",
                "Intermittent",
                "Reducer",
            ],
        };
        var profiles = new Dictionary<string, (int Speed, string Skills)>
        {
            ["Carrion-Eater"] = (6, "Recon 1"),
            ["Chaser"] = (12, "Athletics (Dexterity) 1;Athletics (Endurance) 1"),
            ["Eater"] = (6, ""),
            ["Filter"] = (3, ""),
            ["Gatherer"] = (9, "Stealth 1"),
            ["Grazer"] = (9, "Recon 1"),
            ["Hunter"] = (12, "Survival 1"),
            ["Hijacker"] = (9, "Melee (Natural) 1"),
            ["Intimidator"] = (9, "Persuade 1"),
            ["Killer"] = (12, "Melee (Natural) 2"),
            ["Intermittent"] = (6, "Recon 1"),
            ["Pouncer"] = (10, "Stealth 1;Recon 1;Athletics (Dexterity) 2"),
            ["Reducer"] = (6, ""),
            ["Siren"] = (1, "Stealth 1;Deception 1"),
            ["Trapper"] = (4, "Stealth 1;Deception 1"),
        };
        foreach (var (kind, options) in behaviours)
        {
            var types = Enumerable
                .Range(0, 4)
                .Select(roll => Generate(kind, 0, 0, 0, roll, 0).AnimalType)
                .ToArray();
            var expectedTypes = kind switch
            {
                AnimalArchetype.Killer => new[]
                {
                    "Carnivore",
                    "Carnivore",
                    "Carnivore",
                    "Carnivore",
                },
                AnimalArchetype.Hunter => ["Carnivore", "Carnivore", "Omnivore", "Omnivore"],
                _ => ["Herbivore", "Herbivore", "Omnivore", "Scavenger"],
            };
            Check(types.SequenceEqual(expectedTypes), "Type weights for " + kind);
            for (var roll = 0; roll < options.Length; roll++)
            {
                var animal = Generate(kind, 0, 0, 0, 0, roll);
                var profile = profiles[options[roll]];
                Check(
                    animal.Description == AnimalGenerator.InitialDescription(options[roll])
                        && animal.Description.StartsWith(options[roll] + " (")
                        && animal.Description.Contains("): "),
                    "Generated description includes the behaviour and Earth example: "
                        + options[roll]
                );
                Check(
                    animal.Behaviour == options[roll]
                        && animal.Character.Speed == profile.Speed
                        && string.Join(
                            ";",
                            animal.Character.Skills!.Select(s => $"{s.Name} {s.Level}")
                        ) == profile.Skills,
                    "Behaviour has the specified Speed and skills: " + options[roll]
                );
            }
        }
        Check(
            AnimalCombat.Trait("Small (-4)", "Small") == 4
                && AnimalCombat.Trait("Large (+6)", "Large") == 6,
            "Signed size traits retain the correct combat modifier magnitude"
        );
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var db = new MtuDbContext(
            new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(connection).Options
        );
        await db.Database.MigrateAsync();
        db.Campaigns.Add(new CampaignEntity { Id = 1, Name = "Generation" });
        await db.SaveChangesAsync();
        var service = new AnimalService(db);
        var generated = Generate(AnimalArchetype.Killer, 0, 0, 1, 0, 2);
        await service.SaveAsync(generated);
        var saved = (await service.ListAsync(1)).Single();
        Check(
            saved.Description == generated.Description
                && saved.Character.Name == generated.Character.Name
                && saved.Hits == 2
                && saved.Behaviour == "Killer"
                && saved.Character.Speed == 12
                && saved.Character.Skills!.Single().Name == "Melee (Natural)"
                && saved.Character.Skills!.Single().Level == 2
                && saved.Traits == "Small (-4)"
                && saved.NaturalAttacks.Single() == new NaturalAttack("Charge", "1"),
            "Generated animal, skills, traits and Charge persist together"
        );
        await service.SaveAttacksAsync(1, saved.Character.Id, [new NaturalAttack("Charge", "1")]);
        Check(
            (await service.ListAsync(1)).Single().NaturalAttacks.Single().Damage == "1",
            "Attack editor accepts the table's flat damage value"
        );
        Console.WriteLine(
            "Animal generation size distribution, Hits ranges, type weights, behaviours, skills, speed and persistence passed."
        );
    }
}
