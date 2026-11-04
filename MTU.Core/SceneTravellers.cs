using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

public sealed partial class SceneService
{
    public Task MoveTravellersHereAsync(int campaignId, int sceneId) =>
        AssignTravellersAsync(campaignId, sceneId, null);

    public async Task<SceneCombatant> AddCharacterAsync(
        int campaignId,
        int sceneId,
        int characterId
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var isNpc =
            await db
                .Characters.Where(c =>
                    c.Id == characterId && c.CampaignId == campaignId && (c.IsNpc || !c.Inactive)
                )
                .Select(c => (bool?)c.IsNpc)
                .SingleOrDefaultAsync()
            ?? throw new ArgumentException("Choose an NPC or active Traveller from this campaign.");
        if (isNpc)
            return await AddNpcInstanceAsync(campaignId, sceneId, characterId);
        await AssignTravellersAsync(campaignId, sceneId, characterId);
        return (await CombatantsAsync(campaignId, sceneId)).Single(e =>
            e.CharacterId == characterId
        );
    }

    private async Task AssignTravellersAsync(int campaignId, int sceneId, int? characterId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await RequireScene(db, campaignId, sceneId);
        var targetJson = await db
            .Scenes.Where(s => s.Id == sceneId && s.CampaignId == campaignId)
            .Select(s => s.EncounterState)
            .SingleAsync();
        var target = targetJson is null
            ? new Encounter().Snapshot()
            : JsonSerializer.Deserialize<EncounterSnapshot>(targetJson)!;
        if (target.Active)
            throw new ArgumentException("Exit the encounter before adding Travellers.");
        target = target with { Attributes = target.Attributes ?? [] };
        var characters = await db
            .Characters.Where(c =>
                c.CampaignId == campaignId
                && !c.IsNpc
                && !c.Inactive
                && (characterId == null || c.Id == characterId)
            )
            .ToListAsync();
        var ids = characters.Select(c => c.Id).ToArray();
        var tokens = await db
            .SceneTokens.Where(t => t.Scene.CampaignId == campaignId && ids.Contains(t.CharacterId))
            .ToListAsync();
        var sourceIds = tokens
            .Where(t => t.SceneId != sceneId)
            .Select(t => t.SceneId)
            .Distinct()
            .ToArray();
        var sources = await db
            .Scenes.Where(s => sourceIds.Contains(s.Id) && s.CampaignId == campaignId)
            .Select(s => new { s.Id, s.EncounterState })
            .ToListAsync();
        var states = sources.ToDictionary(
            s => s.Id,
            s =>
                s.EncounterState is null
                    ? null
                    : JsonSerializer.Deserialize<EncounterSnapshot>(s.EncounterState)
        );
        foreach (var character in characters)
        {
            var instances = tokens
                .Where(t => t.CharacterId == character.Id)
                .OrderBy(t => t.Id)
                .ToArray();
            var keep =
                instances.FirstOrDefault(t => t.SceneId == sceneId) ?? instances.FirstOrDefault();
            if (keep is null)
            {
                keep = new SceneTokenEntity { SceneId = sceneId, CharacterId = character.Id };
                db.SceneTokens.Add(keep);
            }
            else if (keep.SceneId != sceneId)
            {
                // Keep token identity and current health when moving between scenes.
                target.Attributes!.Remove(keep.Id);
                target.HitPoints?.Remove(keep.Id);
                var source = states.GetValueOrDefault(keep.SceneId);
                if (source?.Attributes?.TryGetValue(keep.Id, out var health) == true)
                    target.Attributes[keep.Id] = health;
                else if (source?.HitPoints?.TryGetValue(keep.Id, out var hp) == true)
                {
                    var entry = new SceneCombatant(
                        keep.Id,
                        character.Id,
                        character.Name,
                        false,
                        false,
                        keep.IsVisible,
                        null,
                        null,
                        character.Upp
                    );
                    var maximum = Encounter.MaximumHealth(entry);
                    target.Attributes[keep.Id] = maximum.WithTotal(hp, maximum);
                }
                keep.SceneId = sceneId;
                keep.Column = null;
                keep.Row = null;
            }
            db.SceneTokens.RemoveRange(instances.Where(t => t != keep));
        }
        await db.SaveChangesAsync();
        var json = JsonSerializer.Serialize(target);
        await db
            .Scenes.Where(s => s.Id == sceneId && s.CampaignId == campaignId)
            .ExecuteUpdateAsync(s => s.SetProperty(s => s.EncounterState, json));
        await transaction.CommitAsync();
    }
}
