using System.Text.Json;
using Microsoft.JSInterop;
using MTU.Core;

namespace MTUAssistant.Components.Pages;

public partial class Battlemap
{
    // Experimental and GM-only. The mode is an editing state of this page and is not saved.
    private bool polygonMode;

    private async Task TogglePolygonMode()
    {
        if (map is null)
            return;
        polygonMode = !polygonMode;
        await UpdateMap("setPolygonMode", polygonMode);
    }

    private Task UndoPolygon() => UpdateMap("undoPolygon");

    [JSInvokable]
    public async Task<bool> SavePolygons(
        int campaignId,
        int sceneId,
        long revision,
        int gridSize,
        IJSStreamReference data
    )
    {
        await using var reference = data;
        if (
            disposed
            || BattlemapState.Current is not { } scene
            || scene.Id != sceneId
            || scene.CampaignId != campaignId
            || scene.Revision != revision
            || scene.GridSize != gridSize
        )
            return false;
        try
        {
            await using var stream = await data.OpenReadStreamAsync(4 * 1024 * 1024);
            var polygons =
                await JsonSerializer.DeserializeAsync<ScenePolygon[]>(
                    stream,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                ) ?? throw new ArgumentException("Invalid polygon data.");
            await ScenesStore.SavePolygonsAsync(campaignId, sceneId, revision, gridSize, polygons);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error =
                ex is ArgumentException ? ex.Message : "Unable to save polygons. Please try again.";
            return false;
        }
        finally
        {
            if (!disposed)
                StateHasChanged();
        }
    }
}
