using MTU.Core;

namespace MTUAssistant.Services;

/// <summary>The selected world is shared between pages in one user circuit.</summary>
public sealed class SelectedWorldState
{
    public SelectedWorld? World { get; set; }
}
