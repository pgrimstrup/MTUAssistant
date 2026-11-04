using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

public sealed partial class SceneService
{
    public async Task<EncounterSnapshot?> LoadEncounterAsync(int campaignId, int sceneId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        await RequireScene(db, campaignId, sceneId);
        var json = await db
            .Scenes.AsNoTracking()
            .Where(s => s.Id == sceneId && s.CampaignId == campaignId)
            .Select(s => s.EncounterState)
            .SingleAsync();
        return json is null ? null : JsonSerializer.Deserialize<EncounterSnapshot>(json);
    }

    public async Task SaveEncounterAsync(int campaignId, int sceneId, EncounterSnapshot state)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var json = JsonSerializer.Serialize(state);
        // Keep combat state independent of image revisions, grid changes and token movement.
        var changed = await db
            .Scenes.Where(s => s.Id == sceneId && s.CampaignId == campaignId)
            .ExecuteUpdateAsync(s => s.SetProperty(s => s.EncounterState, json));
        if (changed == 0)
            throw new ArgumentException("Choose a scene from this campaign.");
    }
}
