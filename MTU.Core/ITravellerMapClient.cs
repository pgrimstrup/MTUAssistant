using System;
using System.Collections.Generic;
using System.Text;
using MTU.Core.Models;

namespace MTU.Core;

public interface ITravellerMapClient
{
    Task<SectorMetadata> GetMetadataAsync(string sectorName, string milieu, CancellationToken cancellationToken = default);
    Task<SectorData> GetSectorDataAsync(string sectorName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SectorSummary>> GetSectorsAsync(CancellationToken cancellationToken = default);
}
