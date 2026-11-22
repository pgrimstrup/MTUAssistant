using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

/// <summary>A polygon point in half squares from the map's top-left corner. Even values lie on grid
/// lines and odd values on square centres, so a point is either a square corner (both even) or a
/// square centre (both odd).</summary>
public sealed record PolygonPoint(int X, int Y);

/// <summary>A closed polygon drawn on a scene's Battlemap. Experimental and GM-only: polygons are
/// never sent to the player presentation. An active polygon is solid and blocks line of sight; an
/// inactive one is ignored, like an open door. Polygons stored before the flag existed are active.</summary>
public sealed record ScenePolygon(IReadOnlyList<PolygonPoint> Points, bool Active = true);

public sealed partial class SceneService
{
    public const int MaxPolygons = 200;
    public const int MaxPolygonPoints = 200;

    public async Task<IReadOnlyList<ScenePolygon>> LoadPolygonsAsync(int campaignId, int sceneId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        await RequireScene(db, campaignId, sceneId);
        var json = await db
            .Scenes.AsNoTracking()
            .Where(s => s.Id == sceneId && s.CampaignId == campaignId)
            .Select(s => s.PolygonState)
            .SingleAsync();
        return json is null ? [] : JsonSerializer.Deserialize<ScenePolygon[]>(json) ?? [];
    }

    /// <summary>Replaces the scene's polygons. Like fog, a save is rejected when the scene's image or
    /// grid size has changed since the Battlemap was opened.</summary>
    public async Task SavePolygonsAsync(
        int campaignId,
        int sceneId,
        long revision,
        int gridSize,
        IReadOnlyList<ScenePolygon> polygons
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
        // Points are counted in half squares and must lie on the image.
        var maxX = 2 * image.Width / scene.GridSize;
        var maxY = 2 * image.Height / scene.GridSize;
        if (polygons.Count > MaxPolygons)
            throw new ArgumentException($"A scene can hold up to {MaxPolygons} polygons.");
        if (polygons.Any(p => !ValidPolygon(p, maxX, maxY)))
            throw new ArgumentException(
                "Polygons need at least three points, each on a square corner or centre inside the scene."
            );
        var json =
            polygons.Count == 0
                ? null
                : JsonSerializer.Serialize(
                    polygons.Select(p => new ScenePolygon([.. p.Points], p.Active))
                );
        var changed = await db
            .Scenes.Where(s =>
                s.Id == sceneId
                && s.CampaignId == campaignId
                && s.Revision == revision
                && s.GridSize == gridSize
            )
            .ExecuteUpdateAsync(s => s.SetProperty(s => s.PolygonState, json));
        if (changed == 0)
            throw new ArgumentException("The scene changed. Reopen it before editing polygons.");
    }

    private static bool ValidPolygon(ScenePolygon? polygon, int maxX, int maxY)
    {
        if (polygon?.Points is not { Count: >= 3 and <= MaxPolygonPoints } points)
            return false;
        for (var i = 0; i < points.Count; i++)
        {
            var point = points[i];
            if (
                point is null
                || point.X < 0
                || point.Y < 0
                || point.X > maxX
                || point.Y > maxY
                // A corner has both values even and a centre has both odd.
                || (point.X - point.Y) % 2 != 0
                // Each side joins two different points, including the closing side.
                || point == points[(i + 1) % points.Count]
            )
                return false;
        }
        return true;
    }
}
