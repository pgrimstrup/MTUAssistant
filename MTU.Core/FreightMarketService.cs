using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public sealed class FreightMarketService(MtuDbContext db)
{
    private const string Kind = "Freight";

    public async Task<FreightResult?> GetAsync(
        int sectorId,
        string hex,
        CancellationToken ct = default
    )
    {
        var entity = await db
            .TransportMarkets.AsNoTracking()
            .SingleOrDefaultAsync(
                m => m.SectorId == sectorId && m.Hex == hex && m.Kind == Kind,
                ct
            );
        if (entity is null)
            return null;
        var result =
            JsonSerializer.Deserialize<FreightResult>(entity.ResultJson)
            ?? throw new InvalidDataException("Invalid saved freight data.");
        // Correct the old label without rerolling or modifying saved lot counts.
        return result with
        {
            Lines = result
                .Lines.Select(line =>
                    line.LotSize == "1Dx5 (3 to 30)"
                        ? line with
                        {
                            LotSize = "1Dx5 (5 to 30)",
                        }
                        : line
                )
                .ToArray(),
        };
    }

    public async Task SaveAsync(
        int sectorId,
        string hex,
        FreightResult result,
        CancellationToken ct = default
    )
    {
        var entity = await db.TransportMarkets.SingleOrDefaultAsync(
            m => m.SectorId == sectorId && m.Hex == hex && m.Kind == Kind,
            ct
        );
        if (entity is null)
        {
            entity = new TransportMarketEntity
            {
                SectorId = sectorId,
                Hex = hex,
                Kind = Kind,
            };
            db.TransportMarkets.Add(entity);
        }
        entity.ResultJson = JsonSerializer.Serialize(result);
        await db.SaveChangesAsync(ct);
    }
}
