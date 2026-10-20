using System.Net.Http.Json;
using MTU.Core;
using MTU.Core.Models;

namespace MTU.TravellerMap;

internal class TravellerMapClient(HttpClient httpClient) : ITravellerMapClient
{
    public static readonly Uri DefaultBaseAddress = new("https://travellermap.com/");

    /// <summary>GET /data - all known sectors.</summary>
    public async Task<IReadOnlyList<SectorSummary>> GetSectorsAsync(CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<SectorListResponse>("data", cancellationToken);
        return response?.Sectors ?? [];
    }

    /// <summary>GET /data/{sector}/image?scale={n} - PNG bytes.</summary>
    public async Task<byte[]> GetSectorImageAsync(string sectorName, SectorImageScale scale, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(scale))
            throw new ArgumentOutOfRangeException(nameof(scale));

        return await httpClient.GetByteArrayAsync($"data/{Uri.EscapeDataString(sectorName)}/image?scale={(int)scale}&options=24624", cancellationToken);
    }

    /// <summary>GET /data/{sector}/{subsector}/image?scale={n} - PNG bytes. The subsector is a letter A-P.</summary>
    public async Task<byte[]> GetSubsectorImageAsync(string sectorName, char subsector, SectorImageScale scale, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(scale))
            throw new ArgumentOutOfRangeException(nameof(scale));
        if (subsector is < 'A' or > 'P')
            throw new ArgumentOutOfRangeException(nameof(subsector), "Subsector must be a letter A-P.");

        return await httpClient.GetByteArrayAsync($"data/{Uri.EscapeDataString(sectorName)}/{subsector}/image?scale={(int)scale}&options=24624&sscoords=1", cancellationToken);
    }

    /// <summary>GET /data/{sector} - sector details and all systems.</summary>
    public async Task<SectorData> GetSectorDataAsync(string sectorName, CancellationToken cancellationToken = default)
    {
        var text = await httpClient.GetStringAsync($"data/{Uri.EscapeDataString(sectorName)}", cancellationToken);
        return SectorDataParser.Parse(text);
    }
}
