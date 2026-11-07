using System.Net.Http.Json;
using MTU.Core;
using MTU.Core.Models;

namespace MTU.TravellerMap;

internal class TravellerMapClient(HttpClient httpClient) : ITravellerMapClient
{
    //public static readonly Uri DefaultBaseAddress = new("https://travellermap.com/");
    public static readonly Uri DefaultBaseAddress = new("http://localhost:50103/");

    /// <summary>GET /data - all known sectors.</summary>
    public async Task<IReadOnlyList<SectorSummary>> GetSectorsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var response = await httpClient.GetFromJsonAsync<SectorListResponse>(
            "data",
            cancellationToken
        );
        return response?.Sectors ?? [];
    }

    public async Task<SectorMetadata> GetMetadataAsync(
        string sectorName,
        string milieu,
        CancellationToken cancellationToken = default
    )
    {
        var url =
            $"https://travellermap.com/api/metadata?sector={Uri.EscapeDataString(sectorName)}&milieu={Uri.EscapeDataString(milieu)}";
        return await httpClient.GetFromJsonAsync<SectorMetadata>(url, cancellationToken)
            ?? throw new InvalidDataException($"Empty metadata response for {sectorName}.");
    }

    /// <summary>GET /data/{sector} - sector details and all systems.</summary>
    public async Task<SectorData> GetSectorDataAsync(
        string sectorName,
        CancellationToken cancellationToken = default
    )
    {
        var text = await httpClient.GetStringAsync(
            $"data/{Uri.EscapeDataString(sectorName)}",
            cancellationToken
        );
        return SectorDataParser.Parse(text);
    }
}
