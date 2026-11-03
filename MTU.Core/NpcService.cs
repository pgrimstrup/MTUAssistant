using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public enum NpcSkillLevel
{
    Recruit = 0,
    Novice = 1,
    Adept = 2,
    Practitioner = 3,
    Veteran = 4,
    Expert = 5,
    Master = 6,
    Elite = 7,
    Champion = 8,
    Hero = 9,
    Mythic = 10,
    Legendary = 11,
}

public enum NpcType
{
    AllyEnemy,
    PatronCivilian,
    Combatant,
}

public static class NpcTypes
{
    public static string Label(NpcType type) =>
        type switch
        {
            NpcType.AllyEnemy => "Ally/Enemy",
            NpcType.PatronCivilian => "Patron/Civilian",
            NpcType.Combatant => "Combatant",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
}

public sealed record Npc(
    Character Character,
    string Occupation = "",
    string Gender = "",
    string Quirks1 = "",
    string Quirks2 = "",
    NpcSkillLevel SkillLevel = NpcSkillLevel.Novice,
    int? Age = null,
    NpcType Type = NpcType.AllyEnemy
);

public sealed class NpcService(MtuDbContext db, CharacterService characterStore)
{
    public async Task<IReadOnlyList<Npc>> ListAsync(int campaignId, bool equippedOnly = false)
    {
        var characters = await characterStore.ListAsync(
            campaignId,
            npcs: true,
            equippedOnly: equippedOnly
        );
        var details = await db
            .NPCs.AsNoTracking()
            .Where(p => p.Character.CampaignId == campaignId && p.Character.IsNpc)
            .ToDictionaryAsync(p => p.CharacterId);
        return characters
            .Where(c => details.ContainsKey(c.Id))
            .Select(c =>
            {
                var p = details[c.Id];
                return new Npc(
                    c,
                    p.Occupation,
                    p.Gender,
                    p.Quirks1,
                    p.Quirks2,
                    p.SkillLevel,
                    p.Age,
                    p.Type
                );
            })
            .ToArray();
    }

    public async Task SaveAsync(Npc character, bool replaceSkills = false)
    {
        var actor = character.Character;
        var name = actor.Name.Trim();
        var race = actor.Race.Trim();
        var upp = string.IsNullOrWhiteSpace(actor.Upp)
            ? NpcRaces.SupportsGeneration(race)
                ? "777777"
                : ""
            : actor.Upp.Trim().ToUpperInvariant();
        var occupation = character.Occupation.Trim();
        var gender = character.Gender.Trim();
        var quirks1 = character.Quirks1.Trim();
        var quirks2 = character.Quirks2.Trim();
        if (name.Length is 0 or > 120)
            throw new ArgumentException("Enter a name of up to 120 characters.");
        if (
            race.Length > 120
            || occupation.Length > 120
            || gender.Length > 120
            || upp.Length > 40
            || quirks1.Length > 500
            || quirks2.Length > 500
        )
            throw new ArgumentException("One of the character fields is too long.");
        if (!Enum.IsDefined(character.SkillLevel))
            throw new ArgumentException("Select a valid skill level.");
        if (!Enum.IsDefined(character.Type))
            throw new ArgumentException("Select a valid NPC type.");
        if (character.Age < 0)
            throw new ArgumentException("Age must be zero or greater.");
        // Save generated skills atomically, replacing existing skills only when
        // the caller explicitly requests it after a reroll.
        var initialSkills =
            actor.Id == 0 || replaceSkills
                ? (actor.Skills ?? []).Select(s => s with { Name = s.Name?.Trim() ?? "" }).ToArray()
                : [];
        if (
            initialSkills.Any(s => s.Name.Length is 0 or > 120 || s.Level < 0)
            || initialSkills.Select(s => s.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != initialSkills.Length
        )
            throw new ArgumentException(
                "Initial skills must have distinct names and nonnegative ranks."
            );
        if (
            actor.TokenImage is { } image
            && (image.Length > CharacterToken.MaxBytes || CharacterToken.ContentType(image) is null)
        )
            throw new ArgumentException("Choose a PNG, JPEG, GIF or WebP image up to 2 MB.");
        initialSkills = NpcGenerator.RecordedSkills(initialSkills);
        if (!await db.Campaigns.AnyAsync(c => c.Id == actor.CampaignId))
            throw new ArgumentException("The selected campaign no longer exists.");
        IReadOnlyList<EquipmentItem> initialEquipment = [];
        if (actor.Id == 0 && character.Type == NpcType.Combatant)
        {
            var catalogue = await db.Equipment.AsNoTracking().ToArrayAsync();
            var selected = actor.Equipment ?? CombatantLoadouts.Select(character, catalogue);
            var ids = selected.Select(e => e.Id).Distinct().ToArray();
            initialEquipment = catalogue.Where(e => ids.Contains(e.Id)).ToArray();
            if (initialEquipment.Count != ids.Length)
                throw new ArgumentException(
                    "Selected equipment is no longer in the Supply Catalog. Generate the NPC again."
                );
        }
        CharacterEntity entity;
        NpcEntity details;
        if (actor.Id == 0)
        {
            entity = new CharacterEntity { CampaignId = actor.CampaignId, IsNpc = true };
            details = new NpcEntity { Character = entity };
            db.NPCs.Add(details);
            foreach (var item in initialEquipment)
                db.CharacterEquipment.Add(
                    new CharacterEquipmentEntity
                    {
                        Character = entity,
                        EquipmentId = item.Id,
                        Quantity = 1,
                        Equipped = true,
                    }
                );
            foreach (var skill in initialSkills)
                db.CharacterSkills.Add(
                    new CharacterSkillEntity
                    {
                        Character = entity,
                        Name = skill.Name,
                        Level = skill.Level,
                    }
                );
        }
        else
        {
            details =
                await db
                    .NPCs.Include(p => p.Character)
                    .SingleOrDefaultAsync(p =>
                        p.CharacterId == actor.Id
                        && p.Character.CampaignId == actor.CampaignId
                        && p.Character.IsNpc
                    )
                ?? throw new ArgumentException(
                    "This character is not a patron or enemy in the current campaign."
                );
            entity = details.Character;
            if (replaceSkills)
            {
                var existing = await db
                    .CharacterSkills.Where(s => s.CharacterId == actor.Id)
                    .ToListAsync();
                foreach (
                    var skill in existing.Where(s =>
                        !initialSkills.Any(n =>
                            n.Name.Equals(s.Name, StringComparison.OrdinalIgnoreCase)
                        )
                    )
                )
                    db.CharacterSkills.Remove(skill);
                foreach (var skill in initialSkills)
                {
                    var saved = existing.SingleOrDefault(s =>
                        s.Name.Equals(skill.Name, StringComparison.OrdinalIgnoreCase)
                    );
                    if (saved is not null)
                        saved.Level = skill.Level;
                    else
                        db.CharacterSkills.Add(
                            new CharacterSkillEntity
                            {
                                Character = entity,
                                Name = skill.Name,
                                Level = skill.Level,
                            }
                        );
                }
            }
        }
        entity.Name = name;
        entity.Race = race;
        entity.Upp = upp;
        entity.TokenImage = actor.TokenImage;
        details.Occupation = occupation;
        details.Gender = gender;
        details.Quirks1 = quirks1;
        details.Quirks2 = quirks2;
        details.SkillLevel = character.SkillLevel;
        details.Age = character.Age;
        details.Type = character.Type;
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int campaignId, int characterId)
    {
        var entity =
            await db.Characters.SingleOrDefaultAsync(t =>
                t.Id == characterId && t.CampaignId == campaignId && t.IsNpc
            )
            ?? throw new ArgumentException(
                "This character is not a patron or enemy in the current campaign."
            );
        db.Characters.Remove(entity);
        await db.SaveChangesAsync();
    }
}
