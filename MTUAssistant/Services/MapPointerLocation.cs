namespace MTUAssistant.Services;

/// <summary>Pointer location for the current user's map circuit.</summary>
public sealed class MapPointerLocation
{
    public string? Sector { get; private set; }
    public string? Subsector { get; private set; }
    public event Action? Changed;
    public void Set(string? sector, string? subsector)
    {
        if (Sector == sector && Subsector == subsector) return;
        Sector = sector;
        Subsector = subsector;
        Changed?.Invoke();
    }
}
