using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public enum NpcSkillLevel
{
    Recruit,
    Average,
    Experienced,
    Dangerous,
    Elite,
}

public sealed record Npc(
    Character Character,
    string Occupation = "",
    string Gender = "",
    string Quirks1 = "",
    string Quirks2 = "",
    NpcSkillLevel SkillLevel = NpcSkillLevel.Average
);

public sealed class NpcService(MtuDbContext db, CharacterService characterStore)
{
    public async Task<IReadOnlyList<Npc>> ListAsync(int campaignId)
    {
        var characters = await characterStore.ListAsync(campaignId, npcs: true);
        var details = await db
            .NPCs.AsNoTracking()
            .Where(p => p.Character.CampaignId == campaignId && p.Character.IsNpc)
            .ToDictionaryAsync(p => p.CharacterId);
        return characters
            .Where(c => details.ContainsKey(c.Id))
            .Select(c =>
            {
                var p = details[c.Id];
                return new Npc(c, p.Occupation, p.Gender, p.Quirks1, p.Quirks2, p.SkillLevel);
            })
            .ToArray();
    }

    public async Task SaveAsync(Npc character)
    {
        var actor = character.Character;
        var name = actor.Name.Trim();
        var race = actor.Race.Trim();
        var upp = string.IsNullOrWhiteSpace(actor.Upp)
            ? "777777"
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
        if (
            actor.TokenImage is { } image
            && (image.Length > CharacterToken.MaxBytes || CharacterToken.ContentType(image) is null)
        )
            throw new ArgumentException("Choose a PNG, JPEG, GIF or WebP image up to 2 MB.");
        if (!await db.Campaigns.AnyAsync(c => c.Id == actor.CampaignId))
            throw new ArgumentException("The selected campaign no longer exists.");
        CharacterEntity entity;
        NpcEntity details;
        if (actor.Id == 0)
        {
            entity = new CharacterEntity { CampaignId = actor.CampaignId, IsNpc = true };
            details = new NpcEntity { Character = entity };
            db.NPCs.Add(details);
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
