using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MTU.Core;
using MTU.Core.Data;

public static class WeaponSkillChecks
{
    public static async Task RunAsync()
    {
        static void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new MtuDbContext(
            new DbContextOptionsBuilder<MtuDbContext>().UseSqlite(connection).Options
        );
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261103221959_EncounterPersistence");
        db.Campaigns.Add(new CampaignEntity { Id = 1, Name = "Weapon skills" });
        await db.SaveChangesAsync();
        // Use the historical schema before new character columns are migrated.
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO Characters (Id, CampaignId, Name, PlayerName, Race, Upp, TermsServed, HighestNavalRank, HighestScoutRank, NaturalDr, Inactive, IsNpc) VALUES (1,1,'Traveller','','','','0',0,0,0,0,0)"
        );
        db.Equipment.AddRange(
            new EquipmentItem
            {
                Id = 1,
                Name = "Existing rifle",
                Kind = EquipmentKind.Weapon,
                RequiredSkill = "",
                Range = 100,
            },
            new EquipmentItem
            {
                Id = 2,
                Name = "Existing suit",
                Kind = EquipmentKind.Armour,
                RequiredSkill = "Vacc Suit 1",
            }
        );
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO CharacterEquipment (CharacterId, EquipmentId, Equipped) VALUES (1, 1, 1)"
        );
        await migrator.MigrateAsync();
        Check(
            await db.CharacterEquipment.CountAsync() == 1
                && (await db.Equipment.AsNoTracking().SingleAsync(e => e.Id == 2)).RequiredSkill
                    == "Vacc Suit 1",
            "Nullable skill migration preserves loadouts and armour requirements"
        );
        var store = new EquipmentService(db);
        await store.SaveCharacterDetailsAsync(
            1,
            1,
            new CharacterEquipmentDetails
            {
                Equipment = new() { Id = 1 },
                Equipped = true,
                XRayOutliningAid = true,
            }
        );
        Check(
            (await store.ListDetailsForCharacterAsync(1, 1)).Single().XRayOutliningAid,
            "X-Ray Outlining Aid persists and reloads"
        );
        var weapon = await db.Equipment.AsNoTracking().SingleAsync(e => e.Id == 1);
        Check(
            string.IsNullOrEmpty(weapon.RequiredSkill),
            "Existing weapons are not automatically categorized"
        );
        Check(
            EquipmentService.WeaponRequiredSkills.SequenceEqual(
                new[]
                {
                    "Gun Combat (Archaic)",
                    "Gun Combat (Energy)",
                    "Gun Combat (Slug)",
                    "Melee (Blade)",
                    "Melee (Bludgeon)",
                    "Melee (Unarmed)",
                }
            ),
            "Weapon category choices"
        );
        foreach (var required in EquipmentService.WeaponRequiredSkills)
        {
            weapon.RequiredSkill = required;
            weapon = await store.SaveAsync(weapon);
            Check(
                (await db.Equipment.AsNoTracking().SingleAsync(e => e.Id == 1)).RequiredSkill
                    == required,
                "Weapon category persists: " + required
            );
        }
        foreach (var empty in new string?[] { null, "", "  " })
        {
            weapon.RequiredSkill = empty;
            weapon = await store.SaveAsync(weapon);
            Check(
                (await db.Equipment.AsNoTracking().SingleAsync(e => e.Id == 1)).RequiredSkill
                    is null,
                "Clearing weapon category stores null"
            );
        }
        var legacy = new CharacterEquipmentDetails
        {
            Equipped = true,
            Equipment = new()
            {
                Kind = EquipmentKind.Weapon,
                Range = 100,
                RequiredSkill = "",
            },
        };
        Check(
            !EquipmentSkillRules.LacksEquippedWeaponSkill([], legacy),
            "Blank weapon requirements never produce a warning"
        );
        weapon.RequiredSkill = "Broker";
        var rejected = false;
        try
        {
            await store.SaveAsync(weapon);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Check(rejected, "Invalid weapon skill category rejected");
        var entry = new CharacterEquipmentDetails
        {
            Equipped = true,
            Equipment = new EquipmentItem
            {
                Kind = EquipmentKind.Weapon,
                Range = 100,
                RequiredSkill = "Melee",
            },
        };
        Check(
            !EquipmentSkillRules.LacksEquippedWeaponSkill([new(0, 1, "Melee", 0)], entry),
            "Selected skill category overrides range-based inference"
        );
        Check(
            EquipmentSkillRules.LacksEquippedWeaponSkill(
                [new(0, 1, "Gun Combat (Slug)", 2)],
                entry
            ),
            "Wrong weapon skill family still highlights"
        );
        var slug = new EquipmentItem
        {
            Kind = EquipmentKind.Weapon,
            RequiredSkill = "Gun Combat (Slug)",
        };
        Check(
            EquipmentSkillRules.WeaponTraining([new(0, 1, "Gun Combat (Slug)", 2)], slug)
                == WeaponSkillMatch.Exact,
            "Exact specialization uses default colour"
        );
        Check(
            EquipmentSkillRules.WeaponTraining([new(0, 1, "Gun Combat", 0)], slug)
                == WeaponSkillMatch.Basic,
            "Generic knowledge uses orange"
        );
        Check(
            EquipmentSkillRules.WeaponTraining([new(0, 1, "Gun Combat (Energy)", 3)], slug)
                == WeaponSkillMatch.Basic,
            "Related specialization uses orange"
        );
        Check(
            EquipmentSkillRules.WeaponTraining([new(0, 1, "Jack-of-All-Trades", 3)], slug)
                == WeaponSkillMatch.Unskilled,
            "Jack alone remains red"
        );
        Check(
            EquipmentSkillRules.WeaponTraining([], new EquipmentItem { RequiredSkill = null })
                == WeaponSkillMatch.Exact,
            "No requirement uses default colour"
        );
        Check(
            EquipmentSkillRules.WeaponTraining([new(0, 1, "Melee (Blade)", 2)], entry.Equipment)
                == WeaponSkillMatch.Exact,
            "Generic Melee requirement accepts implied base knowledge"
        );
        Check(
            EquipmentSkillRules.WeaponTraining([new(0, 1, " gun combat(SLUG) ", 1)], slug)
                == WeaponSkillMatch.Exact,
            "Exact matching is case and spacing tolerant"
        );
        Console.WriteLine(
            "Nullable weapon skill migration, existing loadouts, category validation, save/clear and explicit skill checks passed."
        );
    }
}
