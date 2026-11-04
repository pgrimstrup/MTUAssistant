using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core;
using MTU.Core.Data;

public static class SkillChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
        var character = new Character(1, 1, "", "Tester", "Human", "777777", "0", 0, 0, 0);
        int Dm(string required, params CharacterSkill[] skills) =>
            CharacterService.SkillDm(character with { Skills = skills }, required);
        CharacterSkill Skill(string name, int rank) => new(0, 1, name, rank);
        Check(Dm("Broker", Skill("Broker", 3)) == 3, "Generic exact rank");
        Check(
            Dm(
                "Gun Combat (Slug)",
                Skill("Gun Combat (Slug)", 2),
                Skill("Gun Combat (Energy)", 5),
                Skill("Gun Combat", 0)
            ) == 2,
            "Exact specialization takes priority"
        );
        Check(
            Dm("Gun Combat (Slug)", Skill("Gun Combat (Energy)", 5)) == 0,
            "Other specialization gives rank zero"
        );
        Check(
            Dm("Gun Combat (Slug)", Skill("Gun Combat", 0)) == 0,
            "Generic family gives rank zero"
        );
        Check(
            Dm("Gun Combat", Skill("Gun Combat (Energy)", 2)) == 0,
            "Specialist implies generic basic knowledge"
        );
        Check(
            Dm(" Gun Combat (slug) ", Skill("gun combat (SLUG)", 3)) == 3,
            "Case and surrounding whitespace"
        );
        Check(
            Dm("Gun Combat (Slug)", Skill("Gunner (Turret)", 3)) == -3,
            "Unrelated skill does not match"
        );
        Check(
            Dm("Science (New field)", Skill("Science (Physics)", 2)) == 0,
            "Custom specializations share family"
        );
        for (var jack = 0; jack <= 5; jack++)
        {
            Check(
                Dm("Gun Combat (Slug)", Skill("Jack-of-All-Trades", jack))
                    == -3 + Math.Min(3, jack),
                "Jack reduces only the unskilled penalty"
            );
            Check(
                Dm(
                    "Gun Combat (Slug)",
                    Skill("Gun Combat (Energy)", 2),
                    Skill("Jack-of-All-Trades", jack)
                ) == 0,
                "Jack never boosts basic knowledge"
            );
            Check(
                Dm(
                    "Gun Combat (Slug)",
                    Skill("Gun Combat (Slug)", 2),
                    Skill("Jack-of-All-Trades", jack)
                ) == 2,
                "Jack never boosts specialist rank"
            );
        }
        Check(
            SkillRules.Rank([Skill("Jack-of-All-Trades", 3)], "Broker") is null,
            "Jack does not confer training"
        );
        var sheet = NpcGenerator.RecordedSkills([Skill("Gun Combat (Slug)", 2)]);
        Check(
            sheet.Any(s => s.Name == "Gun Combat" && s.Level == 0)
                && sheet.Any(s => s.Name == "Gun Combat (Slug)" && s.Level == 2),
            "Generator records generic zero with trained specialization"
        );

        var armour = new EquipmentItem
        {
            Kind = EquipmentKind.Armour,
            RequiredSkill = "Vacc Suit 2",
        };
        Check(
            !EquipmentSkillRules.MeetsArmourRequirement([Skill("Vacc Suit", 1)], armour),
            "Insufficient armour skill rank"
        );
        Check(
            EquipmentSkillRules.MeetsArmourRequirement([Skill("Vacc Suit", 2)], armour),
            "Exact armour skill requirement"
        );
        Check(
            !EquipmentSkillRules.MeetsArmourRequirement([Skill("Jack-of-All-Trades", 3)], armour),
            "Jack does not meet an armour requirement"
        );
        Check(
            EquipmentSkillRules.MeetsArmourRequirement([], withNoRequirement()),
            "Armour without requirements is usable"
        );
        EquipmentItem withNoRequirement() =>
            new() { Kind = EquipmentKind.Armour, RequiredSkill = "None" };
        var ranged = new CharacterEquipmentDetails
        {
            Equipped = true,
            Equipment = new()
            {
                Kind = EquipmentKind.Weapon,
                Range = 100,
                RequiredSkill = "Gun Combat (Slug)",
            },
        };
        var melee = new CharacterEquipmentDetails
        {
            Equipped = true,
            Equipment = new()
            {
                Kind = EquipmentKind.Weapon,
                Range = 0,
                RequiredSkill = "Melee",
            },
        };
        Check(
            EquipmentSkillRules.LacksEquippedWeaponSkill([], ranged)
                && EquipmentSkillRules.LacksEquippedWeaponSkill([], melee),
            "Equipped weapons flag absent training"
        );
        Check(
            !EquipmentSkillRules.LacksEquippedWeaponSkill(
                [Skill("Gun Combat (Energy)", 2)],
                ranged
            ),
            "Any Gun Combat specialization grants basic weapon knowledge"
        );
        Check(
            !EquipmentSkillRules.LacksEquippedWeaponSkill([Skill("Melee", 0)], melee),
            "Generic Melee zero meets weapon training"
        );
        Check(
            EquipmentSkillRules.LacksEquippedWeaponSkill([Skill("Jack-of-All-Trades", 3)], ranged),
            "Jack alone still lacks weapon training"
        );
        ranged.Equipped = false;
        Check(
            !EquipmentSkillRules.LacksEquippedWeaponSkill([], ranged),
            "Unequipped weapons have no skill warning"
        );
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddDbContext<MtuDbContext>(o => o.UseSqlite(connection));
        services.AddScoped<SceneService>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        await db.Database.EnsureCreatedAsync();
        db.Campaigns.Add(new CampaignEntity { Id = 1, Name = "Skills" });
        db.Characters.AddRange(
            new CharacterEntity
            {
                Id = 1,
                CampaignId = 1,
                Name = "Tester",
            },
            new CharacterEntity
            {
                Id = 2,
                CampaignId = 1,
                Name = "Naval tactician",
                IsNpc = true,
                Upp = "777777",
            }
        );
        db.Scenes.Add(
            new SceneEntity
            {
                Id = 1,
                CampaignId = 1,
                Name = "Skills",
                Image = [1],
                ContentType = "image/png",
                GridSize = 100,
            }
        );
        db.CharacterSkills.Add(
            new CharacterSkillEntity
            {
                CharacterId = 2,
                Name = "Tactics (Naval)",
                Level = 2,
            }
        );
        await db.SaveChangesAsync();
        var store = new CharacterService(db);
        async Task Reject(string name, int rank)
        {
            try
            {
                await store.SaveSkillAsync(1, Skill(name, rank));
            }
            catch (ArgumentException)
            {
                return;
            }
            throw new Exception("Invalid rank accepted: " + name);
        }
        await Reject("Broker", 6);
        await Reject("Jack-of-All-Trades", 4);
        await Reject("Gun Combat", 1);
        await store.SaveSkillAsync(1, Skill("Gun Combat (Slug)", 0));
        Check(
            (await store.ListSkillsAsync(1, 1)).Single().Name == "Gun Combat",
            "Rank zero saved as generic family"
        );
        await store.SaveSkillAsync(1, Skill("Gun Combat (Slug)", 2));
        await store.SaveSkillAsync(1, Skill("Broker", 5));
        await store.SaveSkillAsync(1, Skill("Jack-of-All-Trades", 3));
        var scenes = scope.ServiceProvider.GetRequiredService<SceneService>();
        var token = await scenes.AddNpcInstanceAsync(1, 1, 2);
        Check(token.MilitaryTactics == 0, "NPC insertion applies other-specialization fallback");
        var placed = await scenes.PositionCombatantAsync(1, 1, token.Id, 2, 3);
        Check(
            placed.MilitaryTactics == 0
                && (await scenes.CombatantsAsync(1, 1)).Single().MilitaryTactics == 0,
            "Scene updates and reloads preserve inferred skill rank"
        );
        var encounter = new Encounter();
        encounter.Begin([placed], true, true, 0, new Dictionary<int, int>(), () => 7);
        Check(
            encounter.EnemyTactics == -1,
            "NPC Tactics check uses basic rank zero from Naval skill"
        );
        Check(
            Encounter.TravellerTacticsLevel(placed with { MilitaryTactics = 1, Tactics = 5 }) == 1,
            "Specific Tactics takes priority over legacy generic ranks"
        );
        Console.WriteLine(
            "Skill specialization priority, generic fallback, Jack penalties, rank limits, generation and scene Tactics checks passed."
        );
    }
}
