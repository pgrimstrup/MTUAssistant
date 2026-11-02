using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public sealed record Traveller(int Id, int CampaignId, string PlayerName, string TravellerName, string Race,
    string Upp, string TermsServed, int HighestNavalRank, int HighestScoutRank, int NaturalDr, byte[]? TokenImage = null, IReadOnlyList<TravellerSkill>? Skills = null);

public sealed record TravellerSkill(int Id, int TravellerId, string Name, int Level);

public sealed class TravellerService(MtuDbContext db)
{
    public async Task<IReadOnlyList<Traveller>> ListAsync(int campaignId)
    {
        var travellers = await db.Travellers.AsNoTracking().Where(t => t.CampaignId == campaignId)
            .OrderBy(t => t.TravellerName).ThenBy(t => t.Id)
            .Select(t => new Traveller(t.Id, t.CampaignId, t.PlayerName, t.TravellerName, t.Race, t.Upp,
                t.TermsServed, t.HighestNavalRank, t.HighestScoutRank, t.NaturalDr, t.TokenImage)).ToArrayAsync();
        var skills = await db.TravellerSkills.AsNoTracking().Where(s => s.Traveller.CampaignId == campaignId)
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Select(s => new TravellerSkill(s.Id, s.TravellerId, s.Name, s.Level)).ToArrayAsync();
        var byTraveller = skills.ToLookup(s => s.TravellerId);
        return travellers.Select(t => t with { Skills = byTraveller[t.Id].ToArray() }).ToArray();
    }

    public async Task SaveAsync(Traveller traveller)
    {
        var name = traveller.TravellerName?.Trim() ?? "";
        var player = traveller.PlayerName?.Trim() ?? "";
        var race = traveller.Race?.Trim() ?? "";
        var upp = traveller.Upp?.Trim().ToUpperInvariant() ?? "";
        if (name.Length is 0 or > 120) throw new ArgumentException("Enter a Traveller name of up to 120 characters.");
        if (player.Length > 120 || race.Length > 120 || upp.Length > 40) throw new ArgumentException("One of the text fields is too long.");
        var terms = traveller.TermsServed?.Trim() ?? "";
        if (terms.Length > 240) throw new ArgumentException("Terms served must be at most 240 characters.");
        if (traveller.HighestNavalRank < 0 || traveller.HighestScoutRank < 0 || traveller.NaturalDr < 0)
            throw new ArgumentException("Ranks and Natural DR must be zero or greater.");
        if (traveller.TokenImage is { } image && (image.Length > TravellerToken.MaxBytes || TravellerToken.ContentType(image) is null))
            throw new ArgumentException("Choose a PNG, JPEG, GIF or WebP image up to 2 MB.");
        if (!await db.Campaigns.AnyAsync(c => c.Id == traveller.CampaignId)) throw new ArgumentException("The selected campaign no longer exists.");
        var entity = traveller.Id == 0 ? new TravellerEntity { CampaignId = traveller.CampaignId }
            : await db.Travellers.SingleAsync(t => t.Id == traveller.Id && t.CampaignId == traveller.CampaignId);
        if (traveller.Id == 0) db.Travellers.Add(entity);
        entity.PlayerName = player; entity.TravellerName = name; entity.Race = race; entity.Upp = upp;
        entity.TermsServed = terms; entity.HighestNavalRank = traveller.HighestNavalRank;
        entity.HighestScoutRank = traveller.HighestScoutRank; entity.NaturalDr = traveller.NaturalDr;
        entity.TokenImage = traveller.TokenImage;
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<TravellerSkill>> ListSkillsAsync(int campaignId, int travellerId) =>
        await db.TravellerSkills.AsNoTracking().Where(s => s.TravellerId == travellerId && s.Traveller.CampaignId == campaignId)
            .OrderBy(s => s.Name).ThenBy(s => s.Id).Select(s => new TravellerSkill(s.Id, s.TravellerId, s.Name, s.Level)).ToArrayAsync();

    public async Task SaveSkillAsync(int campaignId, TravellerSkill skill)
    {
        var name = skill.Name?.Trim() ?? "";
        if (name.Length is 0 or > 120) throw new ArgumentException("Enter a skill name of up to 120 characters.");
        if (skill.Level < 0) throw new ArgumentException("Skill level must be zero or greater.");
        if (!await db.Travellers.AnyAsync(t => t.Id == skill.TravellerId && t.CampaignId == campaignId))
            throw new ArgumentException("This Traveller is not in the current campaign.");
        if (await db.TravellerSkills.AnyAsync(s => s.TravellerId == skill.TravellerId && s.Id != skill.Id && s.Name == name))
            throw new ArgumentException("This Traveller already has a skill with that name.");
        var entity = skill.Id == 0 ? new TravellerSkillEntity { TravellerId = skill.TravellerId }
            : await db.TravellerSkills.SingleAsync(s => s.Id == skill.Id && s.TravellerId == skill.TravellerId && s.Traveller.CampaignId == campaignId);
        if (skill.Id == 0) db.TravellerSkills.Add(entity);
        entity.Name = name; entity.Level = skill.Level;
        await db.SaveChangesAsync();
    }

    public async Task DeleteSkillAsync(int campaignId, int travellerId, int skillId)
    {
        var entity = await db.TravellerSkills.SingleAsync(s => s.Id == skillId && s.TravellerId == travellerId && s.Traveller.CampaignId == campaignId);
        db.TravellerSkills.Remove(entity);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int campaignId, int id)
    {
        var entity = await db.Travellers.SingleAsync(t => t.Id == id && t.CampaignId == campaignId);
        db.Travellers.Remove(entity);
        await db.SaveChangesAsync();
    }
}
