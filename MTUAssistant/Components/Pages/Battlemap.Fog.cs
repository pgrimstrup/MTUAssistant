using System.Text.Json;
using Microsoft.JSInterop;
using MTU.Core;

namespace MTUAssistant.Components.Pages;

public partial class Battlemap
{
    private int fogBrush = 3;
    private bool fogEnabled;

    private async Task ToggleFog()
    {
        if (map is null || busy || BattlemapState.Current is not { } scene)
            return;
        busy = true;
        try
        {
            var enabled = !fogEnabled;
            await ScenesStore.SetFogEnabledAsync(scene.CampaignId, scene.Id, enabled);
            fogEnabled = enabled;
            var cells = enabled ? await ScenesStore.LoadFogAsync(scene.CampaignId, scene.Id) : null;
            await map.InvokeVoidAsync("setFogEnabled", cells);
            error = null;
        }
        catch (JSDisconnectedException) { }
        catch (Exception ex)
        {
            error =
                ex is ArgumentException ? ex.Message : "Unable to change fog. Please try again.";
        }
        finally
        {
            busy = false;
        }
    }

    // Experimental. The scene's Ambient Light in metres, saved with the scene. With each
    // Traveller's own Visual Distance it sets how far line of sight clears fog; 0 for both leaves
    // the fog to the GM.
    private int ambientVisualDistance;

    private async Task SaveAmbientVisualDistance()
    {
        if (map is null || BattlemapState.Current is not { } scene)
            return;
        try
        {
            await ScenesStore.SetAmbientVisualDistanceAsync(
                scene.CampaignId,
                scene.Id,
                ambientVisualDistance
            );
            await UpdateMap("setAmbientSight", ambientVisualDistance);
            error = null;
        }
        catch (Exception ex)
        {
            error =
                ex is ArgumentException
                    ? ex.Message
                    : "Unable to save the Ambient Light. Please try again.";
            // Show the stored value again rather than the one that was refused.
            try
            {
                ambientVisualDistance = await ScenesStore.AmbientVisualDistanceAsync(
                    scene.CampaignId,
                    scene.Id
                );
            }
            catch (Exception) { }
        }
    }

    private Task SetFogBrush() => UpdateMap("setFogBrush", fogBrush);

    [JSInvokable]
    public Task FogBrushChanged(int size) =>
        InvokeAsync(() =>
        {
            if (disposed || size is not (1 or 3 or 5 or 9))
                return;
            fogBrush = size;
            StateHasChanged();
        });

    private Task UndoFog() => UpdateMap("undoFog");

    [JSInvokable]
    public async Task<bool> SaveFog(
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
            await using var stream = await data.OpenReadStreamAsync(64 * 1024 * 1024);
            var cells =
                await JsonSerializer.DeserializeAsync<FogCell[]>(
                    stream,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)
                ) ?? throw new ArgumentException("Invalid fog data.");
            await ScenesStore.SaveFogAsync(campaignId, sceneId, revision, gridSize, cells);
            error = null;
            return true;
        }
        catch (Exception)
        {
            error = "Unable to save fog. Please try again.";
            return false;
        }
        finally
        {
            if (!disposed)
                StateHasChanged();
        }
    }
}
