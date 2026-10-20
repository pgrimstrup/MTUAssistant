using System;
using System.Collections.Generic;
using System.Text;
using MTU.Core.Models;

namespace MTU.Core;

public interface ITravellerMapClient
{
    Task<SectorData> GetSectorDataAsync(string sectorName, CancellationToken cancellationToken = default);
    Task<byte[]> GetSectorImageAsync(string sectorName, SectorImageScale scale, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectorSummary>> GetSectorsAsync(CancellationToken cancellationToken = default);
    Task<byte[]> GetSubsectorImageAsync(string sectorName, char subsector, SectorImageScale scale, CancellationToken cancellationToken = default);
}
