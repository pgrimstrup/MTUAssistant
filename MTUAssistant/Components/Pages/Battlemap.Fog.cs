using System.Text.Json;
using Microsoft.JSInterop;
using MTU.Core;

namespace MTUAssistant.Components.Pages;

public partial class Battlemap
{
    private int fogBrush = 3;

    private async Task SetFogBrush()
    {
        if (map is null)
            return;
        await map.InvokeVoidAsync("setFogBrush", fogBrush);
    }

    [JSInvokable]
    public Task FogBrushChanged(int size) =>
        InvokeAsync(() =>
        {
            if (disposed || size is not (1 or 3 or 5 or 9))
                return;
            fogBrush = size;
            StateHasChanged();
        });

    private Task UndoFog() =>
        map is null ? Task.CompletedTask : map.InvokeVoidAsync("undoFog").AsTask();

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
