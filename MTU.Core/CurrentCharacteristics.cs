using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

/// <summary>A character's STR, DEX and END as the Combat Tracker last left them. Instances is how
/// many tokens the character has across the campaign's scenes. Health is null when nothing is
/// recorded, so the characteristics are at full, or when there is more than one token, since each
/// NPC instance takes damage on its own and no single value applies.</summary>
public sealed record CurrentCharacteristics(int Instances, CombatHealth? Health)
{
    public static CurrentCharacteristics None { get; } = new(0, null);

    /// <summary>The current value of a physical characteristic (0 STR, 1 DEX, 2 END), or the
    /// score itself when no damage is recorded. Null when it cannot be told: no score, another
    /// characteristic, or several instances.</summary>
    public int? Value(int index, int? score)
    {
        if (index is < 0 or > 2 || score is null || Instances > 1)
            return null;
        if (Health is not { } health)
            return score;
        return index switch
        {
            0 => health.Strength,
            1 => health.Dexterity,
            _ => health.Endurance,
        };
    }
}

public sealed partial class SceneService
{
    public async Task<CurrentCharacteristics> CurrentCharacteristicsAsync(
        int campaignId,
        int characterId
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var tokens = await db
            .SceneTokens.AsNoTracking()
            .Where(t =>
                t.CharacterId == characterId
                && t.Scene.CampaignId == campaignId
                && t.Character.CampaignId == campaignId
                && !t.Character.IsAnimal
            )
            .Select(t => new
            {
                t.Id,
                t.Scene.EncounterState,
                t.Character.Upp,
            })
            .Take(2)
            .ToListAsync();
        if (tokens.Count != 1)
            return new(tokens.Count, null);
        var token = tokens[0];
        if (token.EncounterState is not { } json)
            return new(1, null);
        var state = JsonSerializer.Deserialize<EncounterSnapshot>(json);
        if (state?.Attributes?.TryGetValue(token.Id, out var health) == true)
            return new(1, health);
        // Older scenes kept only a hit point total. Spread it as the Combat Tracker does.
        if (state?.HitPoints?.TryGetValue(token.Id, out var hp) == true)
        {
            var (scores, _) = CharacteristicProfile.Split(token.Upp);
            var maximum = new CombatHealth(scores[0] ?? 0, scores[1] ?? 0, scores[2] ?? 0);
            return new(1, maximum.WithTotal(hp, maximum));
        }
        return new(1, null);
    }
}
