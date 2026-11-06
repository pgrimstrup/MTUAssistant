using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class CombatDescriptionChecks
{
    private static void Check(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    public static async Task RunAsync()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<MtuDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<SceneService>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        await db.Database.MigrateAsync();
        db.Campaigns.AddRange(
            new CampaignEntity { Id = 1, Name = "Home" },
            new CampaignEntity { Id = 2, Name = "Away" }
        );
        await db.SaveChangesAsync();
        var characters = new CharacterService(db);
        await characters.SaveAsync(
            new Character(
                0,
                1,
                "Player",
                "Traveller",
                "Human",
                "A98765",
                "2",
                0,
                0,
                0,
                Description: "First line\nSecond line"
            )
        );
        var traveller = (await characters.ListAsync(1)).Single();
        Check(
            traveller.Description == "First line\nSecond line",
            "Traveller multiline description persists"
        );
        var npcs = new NpcService(db, characters);
        await npcs.SaveAsync(
            new Npc(
                new Character(
                    0,
                    1,
                    "",
                    "Guard",
                    "Human",
                    "987654",
                    "0",
                    0,
                    0,
                    0,
                    Description: "Guard description"
                ),
                Gender: "Female",
                Quirks1: "Alert",
                Age: 35
            )
        );
        var npc = (await npcs.ListAsync(1)).Single();
        Check(npc.Character.Description == "Guard description", "NPC description persists");
        var weapon = new EquipmentItem
        {
            Kind = EquipmentKind.Weapon,
            Name = "Rifle",
            Damage = "3D+2",
            Traits = "Auto 3, Bulky",
        };
        var armour = new EquipmentItem
        {
            Kind = EquipmentKind.Armour,
            Name = "Cloth",
            Protection = 8,
            RequiredSkill = "Vacc Suit 1",
        };
        db.Equipment.AddRange(weapon, armour);
        await db.SaveChangesAsync();
        db.CharacterEquipment.AddRange(
            new CharacterEquipmentEntity
            {
                CharacterId = npc.Character.Id,
                EquipmentId = weapon.Id,
                Equipped = true,
            },
            new CharacterEquipmentEntity
            {
                CharacterId = npc.Character.Id,
                EquipmentId = armour.Id,
                Equipped = true,
            }
        );
        await db.SaveChangesAsync();
        var scenes = scope.ServiceProvider.GetRequiredService<SceneService>();
        var scene = await scenes.SaveAsync(
            new SceneInput { CampaignId = 1, Name = "Test" },
            SceneImageChecks.Png(16, 16)
        );
        await scenes.MoveTravellersHereAsync(1, scene.Id);
        var npcToken = await scenes.AddCharacterAsync(1, scene.Id, npc.Character.Id);
        await scenes.RenameNpcInstanceAsync(1, scene.Id, npcToken.Id, "Guard Alpha");
        var block = await scenes.DescribeCombatantAsync(1, scene.Id, npcToken.Id);
        Check(
            block.Name == "Guard Alpha"
                && block.Description == "Guard description"
                && block.Overview.Single(f => f.Label == "Gender").Value == "Female"
                && block.Overview.Single(f => f.Label == "Age").Value == "35"
                && block.Attributes.Single(f => f.Label == "STR").Value == "9",
            "NPC stat block has instance name, demographics and attributes"
        );
        Check(
            block.Rows.Single(f => f.Label == "Weapons").Value == "Rifle (Auto 3, Bulky)"
                && block.Rows.Single(f => f.Label == "Armour").Value == "Cloth (Vacc Suit 1)"
                && !JsonSerializer.Serialize(block).Contains("3D+2"),
            "Description payload excludes weapon damage and armour protection"
        );
        var travellerToken = (await scenes.CombatantsAsync(1, scene.Id)).Single(e => !e.IsNpc);
        var travellerBlock = await scenes.DescribeCombatantAsync(1, scene.Id, travellerToken.Id);
        Check(
            travellerBlock.Description == traveller.Description
                && travellerBlock.Attributes.Single(f => f.Label == "STR").Value == "10",
            "Traveller stat block decodes extended attributes and description"
        );
        var animals = new AnimalService(db);
        await animals.SaveAsync(
            new Animal(
                new Character(0, 1, "", "Beast", "Animal", "", "0", 0, 0, 0, Speed: 9),
                20,
                "Carnivore",
                "Hunter",
                "Large (+1)",
                [new NaturalAttack("Bite", "2D")],
                "Animal description",
                true
            )
        );
        var animal = (await animals.ListAsync(1)).Single();
        var away = await scenes.SaveAsync(
            new SceneInput { CampaignId = 2, Name = "Away" },
            SceneImageChecks.Png(16, 16)
        );
        var beast = await scenes.AddCharacterAsync(2, away.Id, animal.Character.Id);
        var animalBlock = await scenes.DescribeCombatantAsync(2, away.Id, beast.Id);
        Check(
            animalBlock.IsAnimal
                && animalBlock.Overview.Single(f => f.Label == "Hits").Value == "20"
                && animalBlock.Overview.Single(f => f.Label == "Speed").Value == "9m"
                && animalBlock.Description == "Animal description"
                && animalBlock.Rows.Single(f => f.Label == "Attacks").Value == "Bite"
                && animalBlock.Rows.Single(f => f.Label == "Behaviour").Value
                    == "Carnivore, Hunter",
            "Shared animals use the animal layout and description in any campaign"
        );
        var rejected = false;
        try
        {
            await scenes.DescribeCombatantAsync(1, away.Id, beast.Id);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Check(rejected, "Descriptions respect scene campaign boundaries");
        await characters.SaveAsync(traveller with { Description = "" });
        await npcs.SaveAsync(
            npc with
            {
                Character = npc.Character with { Description = "Updated" },
            }
        );
        Check(
            (await characters.ListAsync(1)).Single().Description == ""
                && (await npcs.ListAsync(1)).Single().Character.Description == "Updated",
            "Editors can update and clear descriptions"
        );
        Console.WriteLine(
            "Combat descriptions, demographics, attributes, gear privacy, shared animals and campaign isolation passed."
        );
    }
}
