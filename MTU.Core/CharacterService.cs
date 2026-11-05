using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public sealed record Character(
    int Id,
    int CampaignId,
    string PlayerName,
    string Name,
    string Race,
    string Upp,
    string TermsServed,
    int HighestNavalRank,
    int HighestScoutRank,
    int NaturalDr,
    byte[]? TokenImage = null,
    IReadOnlyList<CharacterSkill>? Skills = null,
    bool Inactive = false,
    IReadOnlyList<EquipmentItem>? Equipment = null,
    bool IsNpc = false,
    InjuryTolerance InjuryTolerance = InjuryTolerance.None
);

public sealed record CharacterSkill(int Id, int CharacterId, string Name, int Level);

public sealed record StewardModifier(int? Value, bool HasSkill, IReadOnlyList<string> MissingInt);

public sealed class CharacterService(MtuDbContext db)
{
    public async Task<IReadOnlyList<Character>> ListAsync(
        int campaignId,
        bool npcs = false,
        bool equippedOnly = false
    )
    {
        var characters = await db
            .Characters.AsNoTracking()
            .Where(t => t.CampaignId == campaignId && t.IsNpc == npcs)
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .Select(t => new Character(
                t.Id,
                t.CampaignId,
                t.PlayerName,
                t.Name,
                t.Race,
                t.Upp,
                t.TermsServed,
                t.HighestNavalRank,
                t.HighestScoutRank,
                t.NaturalDr,
                t.TokenImage,
                null,
                t.Inactive,
                null,
                t.IsNpc,
                t.InjuryTolerance
            ))
            .ToArrayAsync();
        var skills = await db
            .CharacterSkills.AsNoTracking()
            .Where(s => s.Character.CampaignId == campaignId && s.Character.IsNpc == npcs)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Select(s => new CharacterSkill(s.Id, s.CharacterId, s.Name, s.Level))
            .ToArrayAsync();
        var byCharacter = skills.ToLookup(s => s.CharacterId);
        var equipment = await db
            .CharacterEquipment.AsNoTracking()
            .Where(e =>
                e.Character.CampaignId == campaignId
                && e.Character.IsNpc == npcs
                && (!equippedOnly || e.Equipped)
            )
            .OrderBy(e => e.Equipment.Kind)
            .ThenBy(e => e.Equipment.Name)
            .ThenBy(e => e.Equipment.Tl)
            .ThenBy(e => e.EquipmentId)
            .Select(e => new { e.CharacterId, e.Equipment })
            .ToArrayAsync();
        var byEquipment = equipment.ToLookup(e => e.CharacterId, e => e.Equipment);
        return characters
            .Select(t =>
                t with
                {
                    Skills = byCharacter[t.Id].ToArray(),
                    Equipment = byEquipment[t.Id].ToArray(),
                }
            )
            .ToArray();
    }

    public async Task<(int Rank, int? SocDm)> MailModifiersAsync(int campaignId)
    {
        var characters = await db
            .Characters.AsNoTracking()
            .Where(t => t.CampaignId == campaignId && !t.Inactive && !t.IsNpc)
            .Select(t => new
            {
                t.HighestNavalRank,
                t.HighestScoutRank,
                t.Upp,
            })
            .ToArrayAsync();
        var rank = characters
            .Select(t => Math.Max(t.HighestNavalRank, t.HighestScoutRank))
            .DefaultIfEmpty(0)
            .Max();
        var soc = characters.Select(t => SocialDm(t.Upp)).Where(dm => dm.HasValue).Max();
        return (rank, soc);
    }

    public static int? SocialDm(string? upp) => CharacteristicDm(upp, 5);

    public static int? IntelligenceDm(string? upp) => CharacteristicDm(upp, 3);

    public static int SkillDm(Character character, string requiredSkill) =>
        SkillRules.Dm(character.Skills ?? [], requiredSkill);

    public static int? EducationDm(string? upp) => CharacteristicDm(upp, 4);

    public static int? TradingModifier(IEnumerable<Character> characters, string mode)
    {
        if (mode is not ("Broker" or "Streetwise"))
            throw new ArgumentException("Unknown trading mode", nameof(mode));
        return characters
            .Where(t => !t.Inactive && !t.IsNpc)
            .Select(t =>
            {
                var intelligence = IntelligenceDm(t.Upp) ?? 0;
                var education = EducationDm(t.Upp) ?? 0;
                var skill = SkillDm(t, mode);
                return (int?)(skill + Math.Max(intelligence, education));
            })
            .Max();
    }

    private static int? CharacteristicDm(string? upp, int index)
    {
        var profile = upp?.Trim().ToUpperInvariant() ?? "";
        if (profile.Length <= index)
            return null;
        var score = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ".IndexOf(profile[index]);
        return score switch
        {
            < 0 => null,
            0 => -3,
            <= 2 => -2,
            <= 5 => -1,
            <= 8 => 0,
            <= 11 => 1,
            <= 14 => 2,
            _ => 3,
        };
    }

    public async Task<StewardModifier> StewardModifierAsync(int campaignId)
    {
        var active = (await ListAsync(campaignId)).Where(t => !t.Inactive && !t.IsNpc).ToArray();
        var trained = active
            .Where(t =>
                (t.Skills ?? []).Any(s =>
                    s.Name.Equals("Steward", StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToArray();
        var hasSkill = trained.Length > 0;
        var candidates = hasSkill ? trained : active;
        var missing = candidates
            .Where(t => IntelligenceDm(t.Upp) is null)
            .Select(t => t.Name)
            .ToArray();
        var best = candidates
            .Select(t =>
                IntelligenceDm(t.Upp) is { } dm ? (int?)(SkillDm(t, "Steward") + dm) : null
            )
            .Max();
        return new(best, hasSkill, missing);
    }

    public async Task SaveAsync(Character character)
    {
        InjuryToleranceRules.Validate(character.InjuryTolerance);
        var name = character.Name?.Trim() ?? "";
        var player = character.PlayerName?.Trim() ?? "";
        var race = character.Race?.Trim() ?? "";
        var upp = character.Upp?.Trim().ToUpperInvariant() ?? "";
        if (name.Length is 0 or > 120)
            throw new ArgumentException("Enter a Traveller name of up to 120 characters.");
        if (player.Length > 120 || race.Length > 120 || upp.Length > 40)
            throw new ArgumentException("One of the text fields is too long.");
        var terms = character.TermsServed?.Trim() ?? "";
        if (terms.Length > 240)
            throw new ArgumentException("Terms served must be at most 240 characters.");
        if (
            character.HighestNavalRank < 0
            || character.HighestScoutRank < 0
            || character.NaturalDr < 0
        )
            throw new ArgumentException("Ranks and Natural DR must be zero or greater.");
        if (
            character.TokenImage is { } image
            && (image.Length > CharacterToken.MaxBytes || CharacterToken.ContentType(image) is null)
        )
            throw new ArgumentException("Choose a PNG, JPEG, GIF or WebP image up to 2 MB.");
        if (!await db.Campaigns.AnyAsync(c => c.Id == character.CampaignId))
            throw new ArgumentException("The selected campaign no longer exists.");
        var entity =
            character.Id == 0
                ? new CharacterEntity { CampaignId = character.CampaignId }
                : await db.Characters.SingleAsync(t =>
                    t.Id == character.Id && t.CampaignId == character.CampaignId && !t.IsNpc
                );
        if (character.Id == 0)
            db.Characters.Add(entity);
        entity.PlayerName = player;
        entity.Name = name;
        entity.Race = race;
        entity.Upp = upp;
        entity.TermsServed = terms;
        entity.HighestNavalRank = character.HighestNavalRank;
        entity.HighestScoutRank = character.HighestScoutRank;
        entity.NaturalDr = character.NaturalDr;
        entity.InjuryTolerance = character.InjuryTolerance;
        entity.TokenImage = character.TokenImage;
        entity.Inactive = character.Inactive;
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CharacterSkill>> ListSkillsAsync(
        int campaignId,
        int characterId
    ) =>
        await db
            .CharacterSkills.AsNoTracking()
            .Where(s => s.CharacterId == characterId && s.Character.CampaignId == campaignId)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .Select(s => new CharacterSkill(s.Id, s.CharacterId, s.Name, s.Level))
            .ToArrayAsync();

    public async Task SaveSkillAsync(int campaignId, CharacterSkill skill)
    {
        var name = skill.Name?.Trim() ?? "";
        if (name.Length is 0 or > 120)
            throw new ArgumentException("Enter a skill name of up to 120 characters.");
        name = SkillRules.Validate(name, skill.Level);
        if (
            !await db.Characters.AnyAsync(t =>
                t.Id == skill.CharacterId && t.CampaignId == campaignId
            )
        )
            throw new ArgumentException("This Traveller is not in the current campaign.");
        if (
            await db.CharacterSkills.AnyAsync(s =>
                s.CharacterId == skill.CharacterId && s.Id != skill.Id && s.Name == name
            )
        )
            throw new ArgumentException("This Traveller already has a skill with that name.");
        var entity =
            skill.Id == 0
                ? new CharacterSkillEntity { CharacterId = skill.CharacterId }
                : await db.CharacterSkills.SingleAsync(s =>
                    s.Id == skill.Id
                    && s.CharacterId == skill.CharacterId
                    && s.Character.CampaignId == campaignId
                );
        if (skill.Id == 0)
            db.CharacterSkills.Add(entity);
        entity.Name = name;
        entity.Level = skill.Level;
        await db.SaveChangesAsync();
    }

    public async Task DeleteSkillAsync(int campaignId, int characterId, int skillId)
    {
        var entity = await db.CharacterSkills.SingleAsync(s =>
            s.Id == skillId && s.CharacterId == characterId && s.Character.CampaignId == campaignId
        );
        db.CharacterSkills.Remove(entity);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int campaignId, int id)
    {
        var entity = await db.Characters.SingleAsync(t =>
            t.Id == id && t.CampaignId == campaignId && !t.IsNpc
        );
        db.Characters.Remove(entity);
        await db.SaveChangesAsync();
    }
}
