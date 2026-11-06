using Microsoft.JSInterop;

namespace MTUAssistant.Components.Pages;

public partial class Battlemap
{
    private async Task RefreshInstanceNames()
    {
        if (tokenScene is not { } scene)
            return;
        var fresh = await ScenesStore.CombatantsAsync(scene.CampaignId, scene.Id);
        foreach (var updated in fresh)
        {
            var index = entries.FindIndex(e => e.Id == updated.Id);
            if (index < 0 || entries[index].Name == updated.Name)
                continue;
            entries[index] = updated;
            if (map is not null && updated.OnMap)
                await map.InvokeVoidAsync("updateCombatant", updated);
        }
    }
}
