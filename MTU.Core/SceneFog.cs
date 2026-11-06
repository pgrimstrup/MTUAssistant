using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

public sealed record FogCell(int Column, int Row);

public sealed partial class SceneService
{
    public async Task<IReadOnlyList<FogCell>> LoadFogAsync(int campaignId, int sceneId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        await RequireScene(db, campaignId, sceneId);
        var json = await db
            .Scenes.AsNoTracking()
            .Where(s => s.Id == sceneId && s.CampaignId == campaignId)
            .Select(s => s.FogState)
            .SingleAsync();
        return json is null ? [] : JsonSerializer.Deserialize<FogCell[]>(json) ?? [];
    }

    public async Task SaveFogAsync(
        int campaignId,
        int sceneId,
        long revision,
        int gridSize,
        IReadOnlyList<FogCell> cells
    )
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var scene =
            await db
                .Scenes.AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == sceneId && s.CampaignId == campaignId)
            ?? throw new ArgumentException("This scene is not in the selected campaign.");
        var image = SceneImageInfo.Read(scene.Image);
        var columns = (int)Math.Ceiling((double)image.Width / scene.GridSize);
        var rows = (int)Math.Ceiling((double)image.Height / scene.GridSize);
        if (cells.Any(c => c.Column < 0 || c.Row < 0 || c.Column >= columns || c.Row >= rows))
            throw new ArgumentException("Fog cells must be inside the scene.");
        var json = JsonSerializer.Serialize(
            cells.Distinct().OrderBy(c => c.Row).ThenBy(c => c.Column)
        );
        var changed = await db
            .Scenes.Where(s =>
                s.Id == sceneId
                && s.CampaignId == campaignId
                && s.Revision == revision
                && s.GridSize == gridSize
            )
            .ExecuteUpdateAsync(s => s.SetProperty(s => s.FogState, json));
        if (changed == 0)
            throw new ArgumentException("The scene changed. Reopen it before editing fog.");
    }
}
