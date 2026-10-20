using MTU.Core.Models;

namespace MTU.TravellerMap;

internal class SectorListResponse
{
    public List<SectorSummary> Sectors { get; set; } = [];
}
