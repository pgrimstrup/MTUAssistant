using Microsoft.JSInterop;

namespace MTUAssistant.Components.Pages;

public partial class Battlemap
{
    private async Task UpdateMap(string method, params object?[] args)
    {
        if (map is null)
            return;
        try
        {
            await map.InvokeVoidAsync(method, args);
        }
        catch (JSDisconnectedException) { }
    }

    private async Task<bool> FlushTokenMoves()
    {
        if (map is null)
            return true;
        try
        {
            await map.InvokeVoidAsync("flushTokenMoves");
            return true;
        }
        catch (JSDisconnectedException)
        {
            return false;
        }
    }
}
