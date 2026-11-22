using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MTU.Core.Data;

namespace MTU.Core;

/// <summary>Line-of-sight fog on the Battlemap (experimental). Two distances are in play: a scene's
/// Ambient Visual Distance and a character's own Visual Distance, both in metres. The selected
/// Traveller sees as far as the higher of the two, and when both are 0 the fog is left to the GM.
/// The visibility test itself runs in <c>wwwroot/js/LineOfSight.js</c>, beside the fog painting it
/// feeds.</summary>
public static class LineOfSight
{
    /// <summary>The length of one side of a Battlemap square.</summary>
    public const double SquareMetres = 1.5;

    /// <summary>The largest Visual Distance that can be set, in metres.</summary>
    public const int MaxVisualDistance = 1000;

    /// <summary>How far a character sees on a scene, in metres: the higher of the two distances.
    /// 0 means fog is not cleared by line of sight.</summary>
    public static int SightMetres(int ambient, int character) => Math.Max(ambient, character);

    public static void Validate(int metres, string name)
    {
        if (metres is < 0 or > MaxVisualDistance)
            throw new ArgumentException($"{name} must be from 0 to {MaxVisualDistance} metres.");
    }
}

public sealed partial class SceneService
{
    /// <summary>The scene's Ambient Visual Distance in metres. 0 by default.</summary>
    public async Task<int> AmbientVisualDistanceAsync(int campaignId, int sceneId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        return await db
                .Scenes.AsNoTracking()
                .Where(s => s.Id == sceneId && s.CampaignId == campaignId)
                .Select(s => (int?)s.AmbientVisualDistance)
                .SingleOrDefaultAsync()
            ?? throw new ArgumentException("This scene is not in the selected campaign.");
    }

    public async Task SetAmbientVisualDistanceAsync(int campaignId, int sceneId, int metres)
    {
        LineOfSight.Validate(metres, "Ambient Visual Distance");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MtuDbContext>();
        var changed = await db
            .Scenes.Where(s => s.Id == sceneId && s.CampaignId == campaignId)
            .ExecuteUpdateAsync(s => s.SetProperty(s => s.AmbientVisualDistance, metres));
        if (changed == 0)
            throw new ArgumentException("This scene is not in the selected campaign.");
    }
}
