using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public sealed class PassengerMarketService(MtuDbContext db)
{
    private const string Kind = "Passengers";

    public async Task<PassengerResult?> GetAsync(
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
        return JsonSerializer.Deserialize<PassengerResult>(entity.ResultJson)
            ?? throw new InvalidDataException("Invalid saved passenger data.");
    }

    public async Task SaveAsync(
        int sectorId,
        string hex,
        PassengerResult result,
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
