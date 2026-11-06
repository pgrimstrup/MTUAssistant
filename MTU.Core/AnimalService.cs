using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public static class AnimalTypes
{
    public static readonly string[] All = ["Herbivore", "Omnivore", "Carnivore", "Scavenger"];

    public static string Normalize(string type) =>
        type switch
        {
            "Herbivores" => "Herbivore",
            "Omnivores" => "Omnivore",
            "Carnivores" => "Carnivore",
            "Scavengers" => "Scavenger",
            _ => type,
        };

    public static readonly string[] Behaviours =
    [
        "Carrion-Eater",
        "Chaser",
        "Eater",
        "Filter",
        "Gatherer",
        "Grazer",
        "Hunter",
        "Hijacker",
        "Intimidator",
        "Killer",
        "Intermittent",
        "Pouncer",
        "Reducer",
        "Siren",
        "Trapper",
    ];
}

public sealed record NaturalAttack(string Name, string Damage)
{
    public static string FormatDamage(string damage) =>
        System.Text.RegularExpressions.Regex.Replace(
            System
                .Text.RegularExpressions.Regex.Replace(damage.Trim(), @"\s+", "")
                .ToUpperInvariant(),
            @"D6(?!\d)",
            "D"
        );
}

public sealed record Animal(
    Character Character,
    int Hits,
    string AnimalType,
    string Behaviour,
    string Traits,
    IReadOnlyList<NaturalAttack> NaturalAttacks,
    string Description = "",
    bool Shared = false
);

public sealed class AnimalService(MtuDbContext db)
{
    public async Task<IReadOnlyList<Animal>> ListAsync(int campaignId)
    {
        var rows = await db
            .Animals.AsNoTracking()
            .Include(a => a.Character)
            .Where(a => a.Character.CampaignId == campaignId || a.Shared)
            .OrderBy(a => a.Character.Name)
            .ThenBy(a => a.CharacterId)
            .ToArrayAsync();
        var ids = rows.Select(a => a.CharacterId).ToArray();
        var skills = await db
            .CharacterSkills.AsNoTracking()
            .Where(s => ids.Contains(s.CharacterId))
            .OrderBy(s => s.Name)
            .Select(s => new CharacterSkill(s.Id, s.CharacterId, s.Name, s.Level))
            .ToArrayAsync();
        var attacks = await db
            .AnimalAttacks.AsNoTracking()
            .Where(a => ids.Contains(a.CharacterId))
            .OrderBy(a => a.Id)
            .ToArrayAsync();
        return rows.Select(a => new Animal(
                new Character(
                    a.Character.Id,
                    a.Character.CampaignId,
                    "",
                    a.Character.Name,
                    "Animal",
                    "",
                    "0",
                    0,
                    0,
                    0,
                    a.Character.TokenImage,
                    skills.Where(s => s.CharacterId == a.CharacterId).ToArray(),
                    false,
                    [],
                    false,
                    a.Character.InjuryTolerance,
                    a.Character.Speed
                ),
                a.Hits,
                AnimalTypes.Normalize(a.AnimalType),
                a.Behaviour,
                a.Traits,
                attacks
                    .Where(n => n.CharacterId == a.CharacterId)
                    .Select(n => new NaturalAttack(n.Name, NaturalAttack.FormatDamage(n.Damage)))
                    .ToArray(),
                a.Description,
                a.Shared
            ))
            .ToArray();
    }

    public async Task SaveAsync(Animal animal)
    {
        InjuryToleranceRules.Validate(animal.Character.InjuryTolerance);
        var name = animal.Character.Name.Trim();
        if (name.Length is 0 or > 120)
            throw new ArgumentException("Enter an animal name of up to 120 characters.");
        if (animal.Hits < 1)
            throw new ArgumentException("Hits must be at least 1.");
        if (!AnimalTypes.All.Contains(AnimalTypes.Normalize(animal.AnimalType)))
            throw new ArgumentException("Select a valid animal type.");
        if (!AnimalTypes.Behaviours.Contains(animal.Behaviour))
            throw new ArgumentException("Select a valid behaviour.");
        if (animal.Character.Speed < 0)
            throw new ArgumentException("Speed must be zero or greater.");
        if (
            animal.Traits.Length > 1000
            || animal.NaturalAttacks.Any(a =>
                string.IsNullOrWhiteSpace(a.Name)
                || a.Name.Length > 120
                || string.IsNullOrWhiteSpace(a.Damage)
                || a.Damage.Length > 120
            )
        )
            throw new ArgumentException("Enter valid natural attack names and damage values.");
        var skills = (animal.Character.Skills ?? [])
            .Select(s => s with { Name = s.Name.Trim() })
            .ToArray();
        if (
            skills.Any(s => s.Name.Length is 0 or > 120 || s.Level < 0)
            || skills.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != skills.Length
        )
            throw new ArgumentException("Skills must have distinct names and nonnegative ranks.");
        await using var tx = await db.Database.BeginTransactionAsync();
        CharacterEntity character;
        AnimalEntity details;
        if (animal.Character.Id == 0)
        {
            character = new CharacterEntity
            {
                CampaignId = animal.Character.CampaignId,
                IsAnimal = true,
                Name = name,
                Race = "Animal",
                Upp = "",
                NaturalDr = 0,
                Speed = animal.Character.Speed,
                InjuryTolerance = animal.Character.InjuryTolerance,
                TokenImage = animal.Character.TokenImage,
            };
            details = new AnimalEntity { Character = character };
            db.Animals.Add(details);
            foreach (var skill in skills)
                db.CharacterSkills.Add(
                    new CharacterSkillEntity
                    {
                        Character = character,
                        Name = skill.Name,
                        Level = skill.Level,
                    }
                );
        }
        else
        {
            details = await db
                .Animals.Include(a => a.Character)
                .SingleAsync(a =>
                    a.CharacterId == animal.Character.Id
                    && (a.Character.CampaignId == animal.Character.CampaignId || a.Shared)
                );
            character = details.Character;
            character.Name = name;
            character.NaturalDr = 0;
            character.Speed = animal.Character.Speed;
            character.InjuryTolerance = animal.Character.InjuryTolerance;
            character.TokenImage = animal.Character.TokenImage;
            var oldSkills = await db
                .CharacterSkills.Where(s => s.CharacterId == character.Id)
                .ToArrayAsync();
            db.CharacterSkills.RemoveRange(oldSkills);
            foreach (var skill in skills)
                db.CharacterSkills.Add(
                    new CharacterSkillEntity
                    {
                        Character = character,
                        Name = skill.Name,
                        Level = skill.Level,
                    }
                );
        }
        details.Shared = animal.Shared;
        details.Hits = animal.Hits;
        details.AnimalType = AnimalTypes.Normalize(animal.AnimalType);
        details.Behaviour = animal.Behaviour;
        details.Traits = animal.Traits.Trim();
        details.Description = animal.Description;
        if (animal.Character.Id != 0)
        {
            var existing = await db
                .AnimalAttacks.Where(a => a.CharacterId == animal.Character.Id)
                .ToArrayAsync();
            db.AnimalAttacks.RemoveRange(existing);
        }
        foreach (var attack in animal.NaturalAttacks)
            db.AnimalAttacks.Add(
                new AnimalAttackEntity
                {
                    Animal = details,
                    Name = attack.Name.Trim(),
                    Damage = NaturalAttack.FormatDamage(attack.Damage),
                }
            );
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task SaveAttacksAsync(
        int campaignId,
        int characterId,
        IReadOnlyList<NaturalAttack> attacks
    )
    {
        var normalized = attacks
            .Select(a => new NaturalAttack(a.Name.Trim(), NaturalAttack.FormatDamage(a.Damage)))
            .ToArray();
        foreach (var attack in normalized)
        {
            if (attack.Name.Length is 0 or > 120)
                throw new ArgumentException("Enter an attack name of up to 120 characters.");
            if (
                attack.Damage.Length is 0 or > 120
                || !AttackResolution.TryRollDamage(attack.Damage, sides => sides, out _)
            )
                throw new ArgumentException("Enter valid attack damage.");
        }
        await using var tx = await db.Database.BeginTransactionAsync();
        var animal =
            await db.Animals.SingleOrDefaultAsync(a =>
                a.CharacterId == characterId && (a.Character.CampaignId == campaignId || a.Shared)
            ) ?? throw new ArgumentException("Choose an animal in this campaign.");
        var existing = await db
            .AnimalAttacks.Where(a => a.CharacterId == characterId)
            .ToArrayAsync();
        db.AnimalAttacks.RemoveRange(existing);
        foreach (var attack in normalized)
            db.AnimalAttacks.Add(
                new AnimalAttackEntity
                {
                    Animal = animal,
                    Name = attack.Name,
                    Damage = attack.Damage,
                }
            );
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task DeleteAsync(int campaignId, int characterId)
    {
        var animal =
            await db
                .Animals.Include(a => a.Character)
                .SingleOrDefaultAsync(a =>
                    a.CharacterId == characterId
                    && (a.Character.CampaignId == campaignId || a.Shared)
                )
            ?? throw new ArgumentException("Choose an animal in this campaign.");
        db.Characters.Remove(animal.Character);
        await db.SaveChangesAsync();
    }
}
