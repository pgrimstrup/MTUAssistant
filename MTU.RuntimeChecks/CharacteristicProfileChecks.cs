using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MTU.Core;
using MTU.Core.Data;

public static class CharacteristicProfileChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
        static void Rejects(Action action, string message)
        {
            try
            {
                action();
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new Exception(message);
        }

        var (scores, extra) = CharacteristicProfile.Split(" 789abc ");
        Check(
            scores.SequenceEqual(new int?[] { 7, 8, 9, 10, 11, 12 }) && extra == "",
            "UPP splits into six scores, ignoring case and surrounding spaces"
        );
        Check(CharacteristicProfile.Join(scores) == "789ABC", "Scores join back into the same UPP");
        Check(
            CharacteristicProfile.Split("777777").Scores.All(s => s == 7)
                && CharacteristicProfile
                    .Split("F0HJ12")
                    .Scores.SequenceEqual(new int?[] { 15, 0, 17, 18, 1, 2 }),
            "eHex digits skip I and O"
        );
        var (blank, blankExtra) = CharacteristicProfile.Split(null);
        Check(
            blank.Length == 6 && blank.All(s => s is null) && blankExtra == "",
            "A missing UPP has six unknown scores"
        );
        Check(
            CharacteristicProfile.Join(blank) == "" && CharacteristicProfile.Join(blank, " ") == "",
            "Six unknown scores join to an empty UPP"
        );
        var (partial, _) = CharacteristicProfile.Split("78");
        Check(
            partial.SequenceEqual(new int?[] { 7, 8, null, null, null, null })
                && CharacteristicProfile.Join(partial) == "78????",
            "A short UPP keeps its known scores and marks the rest unknown"
        );
        var (unreadable, _) = CharacteristicProfile.Split("7?I777");
        Check(
            unreadable.SequenceEqual(new int?[] { 7, null, null, 7, 7, 7 }),
            "Unreadable digits are unknown scores"
        );
        Check(
            CharacterService.IntelligenceDm("78????") is null
                && CharacterService.IntelligenceDm("789ABC") == 1,
            "An unknown score has no DM, as before"
        );
        var (psionic, psi) = CharacteristicProfile.Split("7777779");
        Check(
            psi == "9" && CharacteristicProfile.Join(psionic, psi) == "7777779",
            "Digits after SOC, such as PSI, are kept"
        );
        Check(
            CharacteristicProfile.Join([15, 0, 33, 7, 7, 7]) == "F0Z777",
            "Scores from 0 to 33 are accepted"
        );
        Rejects(
            () => CharacteristicProfile.Join([7, 7, 7, 7, 7, 34]),
            "A score above 33 was accepted"
        );
        Rejects(
            () => CharacteristicProfile.Join([-1, 7, 7, 7, 7, 7]),
            "A negative score was accepted"
        );
        Rejects(() => CharacteristicProfile.Join([7, 7, 7]), "A short score list was accepted");

        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(connection).Options;
        await using var db = new MtuDbContext(options);
        await db.Database.EnsureCreatedAsync();
        db.Campaigns.Add(new CampaignEntity { Id = 1, Name = "Profiles" });
        await db.SaveChangesAsync();
        var store = new CharacterService(db);
        var id = await store.SaveAsync(
            new Character(0, 1, "", "Newcomer", "", CharacteristicProfile.Join(scores), 0)
        );
        var created = (await store.ListAsync(1)).Single();
        Check(
            id > 0 && created.Id == id && created.Upp == "789ABC",
            "Saving a new Traveller returns its ID"
        );
        await store.SaveSkillAsync(1, new CharacterSkill(0, id, "Broker", 1));
        Check(
            await store.SaveAsync(created with { Name = "Renamed" }) == id
                && (await store.ListAsync(1)).Single()
                    is { Name: "Renamed", Skills: [{ Name: "Broker" }] },
            "Saving an existing Traveller returns the same ID and keeps its skills"
        );
        var npcs = new NpcService(db, store);
        var npcId = await npcs.SaveAsync(
            new Npc(
                new Character(
                    0,
                    1,
                    "",
                    "Contact",
                    "Human",
                    "",
                    0,
                    Skills: [new CharacterSkill(0, 0, "Streetwise", 1)]
                )
            )
        );
        var contact = (await npcs.ListAsync(1)).Single();
        Check(
            npcId > 0
                && npcId != id
                && contact.Character
                    is { Upp: "777777", Skills: [{ Name: "Streetwise", Level: 1 }] }
                && contact.Character.Id == npcId,
            "Saving a new NPC returns its ID, with its generated skills and default UPP stored"
        );
        Check(
            await npcs.SaveAsync(contact with { Gender = "Female" }) == npcId
                && (await npcs.ListAsync(1)).Single()
                    is { Gender: "Female", Character.Skills: [{ Name: "Streetwise" }] },
            "Saving an existing NPC returns the same ID and keeps its skills"
        );
        var animals = new AnimalService(db);
        var animalId = await animals.SaveAsync(
            new Animal(
                new Character(
                    0,
                    1,
                    "",
                    "Kian",
                    "Animal",
                    "",
                    0,
                    Skills: [new CharacterSkill(0, 0, "Recon", 1)]
                ),
                12,
                AnimalTypes.All[0],
                AnimalTypes.Behaviours[0],
                "",
                [new NaturalAttack("Bite", "1D")]
            )
        );
        Check(
            animalId > 0 && (await animals.ListAsync(1)).Single().Character.Id == animalId,
            "Saving a new animal returns its character ID"
        );
        // The Animal dialog's tabs save skills and attacks on their own, then a details save
        // must start from the stored animal so it does not put the old ones back.
        await store.SaveSkillAsync(1, new CharacterSkill(0, animalId, "Survival", 2));
        await animals.SaveAttacksAsync(
            1,
            animalId,
            [new NaturalAttack("Bite", "1D"), new NaturalAttack("Claws", "2D")]
        );
        var stored = (await animals.ListAsync(1)).Single();
        Check(
            await animals.SaveAsync(stored with { Hits = 20 }) == animalId
                && (await animals.ListAsync(1)).Single()
                    is { Hits: 20, NaturalAttacks.Count: 2, Character.Skills.Count: 2 },
            "Saving a re-read animal returns the same ID and keeps skills and attacks added since"
        );
        Console.WriteLine(
            "Characteristic profile split, join, unknown scores, PSI digits, limits and saved Traveller ID checks passed."
        );
    }
}
