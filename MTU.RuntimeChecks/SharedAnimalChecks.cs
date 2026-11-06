using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class SharedAnimalChecks
{
    private static void Check(bool value, string message)
    {
        if (!value)
            throw new Exception(message);
    }

    private static async Task Rejected(Func<Task> action, string message)
    {
        var rejected = false;
        try
        {
            await action();
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Check(rejected, message);
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
        await db.GetService<IMigrator>().MigrateAsync("20261106100000_AnimalDescription");
        db.Campaigns.AddRange(
            new CampaignEntity { Id = 1, Name = "Origin" },
            new CampaignEntity { Id = 2, Name = "Second" },
            new CampaignEntity { Id = 3, Name = "Third" }
        );
        var legacy = new CharacterEntity
        {
            CampaignId = 1,
            Name = "Legacy Animal",
            IsAnimal = true,
        };
        legacy.Id = 1;
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Characters (Id,CampaignId,Name,PlayerName,Race,Upp,TermsServed,HighestNavalRank,HighestScoutRank,NaturalDr,Inactive,IsNpc,IsAnimal,InjuryTolerance,Speed) VALUES (1,1,'Legacy Animal','','','','0',0,0,0,0,0,1,0,6)"
        );
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO Animals (CharacterId, Hits, AnimalType, Behaviour, Traits, Description) VALUES ({legacy.Id}, 10, 'Herbivore', 'Grazer', '', '')"
        );
        await db.Database.MigrateAsync();
        var animals = new AnimalService(db);
        var characters = new CharacterService(db);
        var scenes = scope.ServiceProvider.GetRequiredService<SceneService>();
        var animal = (await animals.ListAsync(1)).Single();
        Check(
            !animal.Shared && !(await animals.ListAsync(2)).Any(),
            "Migration leaves existing animals campaign-only"
        );
        await Rejected(
            () =>
                animals.SaveAsync(
                    animal with
                    {
                        Character = animal.Character with { CampaignId = 2 },
                    }
                ),
            "Private animals cannot be edited from another campaign"
        );
        await Rejected(
            () => animals.DeleteAsync(2, animal.Character.Id),
            "Private animals cannot be deleted from another campaign"
        );
        await animals.SaveAsync(
            animal with
            {
                Shared = true,
                Character = animal.Character with { TokenImage = SceneImageChecks.Png(16, 16) },
            }
        );
        animal = (await animals.ListAsync(2)).Single();
        Check(
            animal.Shared
                && animal.Character.CampaignId == 1
                && (await animals.ListAsync(3)).Single().Character.Id == animal.Character.Id,
            "Shared animals are visible everywhere without changing ownership"
        );
        var sharedId = animal.Character.Id;
        await animals.SaveAsync(
            animal with
            {
                Character = animal.Character with
                {
                    CampaignId = 2,
                    Name = "Shared Beast",
                    Speed = 12,
                },
                Description = "Shared description",
            }
        );
        animal = (await animals.ListAsync(1)).Single();
        Check(
            animal.Character.CampaignId == 1
                && animal.Character.Name == "Shared Beast"
                && animal.Character.Speed == 12,
            "Editing from another campaign updates the shared animal without transferring ownership"
        );
        await characters.SaveSkillAsync(2, new CharacterSkill(0, sharedId, "Melee (Natural)", 2));
        var skill = (await characters.ListSkillsAsync(3, sharedId)).Single();
        await characters.SaveSkillAsync(3, skill with { Level = 3 });
        Check(
            (await characters.ListSkillsAsync(1, sharedId)).Single().Level == 3,
            "Shared animal skills can be listed and edited across campaigns"
        );
        await characters.DeleteSkillAsync(2, sharedId, skill.Id);
        Check(
            !(await characters.ListSkillsAsync(3, sharedId)).Any(),
            "Shared animal skills can be removed from any campaign"
        );
        await animals.SaveAttacksAsync(2, sharedId, [new NaturalAttack("Bite", "2D")]);
        Check(
            (await animals.ListAsync(3)).Single().NaturalAttacks.Single().Name == "Bite",
            "Shared natural attacks can be edited from any campaign"
        );
        Check(
            await scenes.TokenImageAsync(2, sharedId) is not null,
            "Shared token images are available in other campaigns"
        );
        var one = await scenes.SaveAsync(
            new SceneInput { CampaignId = 1, Name = "Home" },
            SceneImageChecks.Png(16, 16)
        );
        var two = await scenes.SaveAsync(
            new SceneInput { CampaignId = 2, Name = "Away" },
            SceneImageChecks.Png(16, 16)
        );
        Check(
            (await scenes.NpcChoicesAsync(2)).Single().CharacterId == sharedId,
            "Shared animals appear in the combat picker"
        );
        var local = await scenes.AddCharacterAsync(1, one.Id, sharedId);
        var remote = await scenes.AddCharacterAsync(2, two.Id, sharedId);
        var remoteTwo = await scenes.AddCharacterAsync(2, two.Id, sharedId);
        Check(
            remote.IsAnimal
                && remoteTwo.Id != remote.Id
                && (await scenes.CombatantsAsync(2, two.Id)).Count == 2,
            "Shared animals support independent multiple instances in other campaigns"
        );
        await scenes.PositionCombatantAsync(2, two.Id, remote.Id, 2, 2);
        await scenes.RenameNpcInstanceAsync(2, two.Id, remote.Id, "Away Beast");
        await scenes.SetCombatantVisibilityAsync(2, two.Id, remote.Id, false);
        remote = (await scenes.CombatantsAsync(2, two.Id)).Single(e => e.Id == remote.Id);
        Check(
            remote.Column == 2 && remote.Name == "Away Beast" && !remote.IsVisible,
            "Shared animal instances support placement, names and visibility"
        );
        await scenes.MoveTravellersHereAsync(2, two.Id);
        Check(
            (await scenes.CombatantsAsync(1, one.Id)).Single().Id == local.Id,
            "Traveller transfer never moves shared animal instances"
        );
        await Rejected(
            () => scenes.PositionCombatantAsync(1, two.Id, remote.Id, 3, 3),
            "Shared animal access does not expose another campaign's scene"
        );
        await animals.SaveAsync(
            animal with
            {
                Shared = false,
                Character = animal.Character with { CampaignId = 2 },
            }
        );
        Check(
            !(await animals.ListAsync(2)).Any()
                && !(await scenes.NpcChoicesAsync(2)).Any()
                && await scenes.TokenImageAsync(2, sharedId) is null,
            "Clearing Shared restores campaign-only access"
        );
        await Rejected(
            () => scenes.AddCharacterAsync(2, two.Id, sharedId),
            "Unshared foreign animals cannot be added to combat"
        );
        await Rejected(
            () => animals.SaveAttacksAsync(2, sharedId, []),
            "Unshared foreign attacks are protected"
        );
        await Rejected(
            () => characters.SaveSkillAsync(2, new CharacterSkill(0, sharedId, "Recon", 1)),
            "Unshared foreign skills are protected"
        );
        await animals.SaveAsync(animal with { Shared = true });
        await scenes.RemoveNpcInstanceAsync(2, two.Id, remoteTwo.Id);
        Check(
            (await scenes.CombatantsAsync(2, two.Id)).Single().Id == remote.Id,
            "Shared instances can be removed from their scene independently"
        );
        await animals.DeleteAsync(3, sharedId);
        Check(
            !(await animals.ListAsync(1)).Any()
                && !(await animals.ListAsync(2)).Any()
                && !(await scenes.CombatantsAsync(1, one.Id)).Any()
                && !(await scenes.CombatantsAsync(2, two.Id)).Any(),
            "Deleting a shared animal from any campaign removes it and its scene instances everywhere"
        );
        Console.WriteLine(
            "Shared animal migration, cross-campaign access, skills, attacks, tokens, combat instances and deletion passed."
        );
    }
}
