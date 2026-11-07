namespace MTU.Core;

public sealed record TradeDestination(CatalogueSystem System, CatalogueSector Sector, int Distance);

public static class TradeDestinations
{
    /// <summary>Hex distance uses the map's odd-column stagger, including across sector seams.</summary>
    public static int? Distance(
        CatalogueSector aSector,
        string aHex,
        CatalogueSector bSector,
        string bHex
    )
    {
        static (int Q, int R)? Position(CatalogueSector sector, string hex)
        {
            if (
                hex.Length != 4
                || !int.TryParse(hex[..2], out var x)
                || !int.TryParse(hex[2..], out var y)
                || x is < 1 or > 32
                || y is < 1 or > 40
            )
                return null;
            var q = sector.X * 32 + x - 1;
            return (q, sector.Y * 40 + y - 1 - (int)Math.Floor(q / 2.0));
        }
        var a = Position(aSector, aHex);
        var b = Position(bSector, bHex);
        if (a is null || b is null)
            return null;
        var dq = a.Value.Q - b.Value.Q;
        var dr = a.Value.R - b.Value.R;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2;
    }

    public static IReadOnlyList<TradeDestination> Within(
        SystemCatalogue catalogue,
        int sectorId,
        string hex,
        int maxDistance = 6
    )
    {
        if (!catalogue.SectorsById.TryGetValue(sectorId, out var origin))
            return [];
        return catalogue
            .Systems.Select(system =>
            {
                var sector = catalogue.SectorsById[system.SectorId];
                var distance =
                    sector.Milieu == origin.Milieu
                        ? Distance(origin, hex, sector, system.Hex)
                        : null;
                return (system, sector, distance);
            })
            .Where(item => item.distance is > 0 && item.distance <= maxDistance)
            .Select(item => new TradeDestination(item.system, item.sector, item.distance!.Value))
            .OrderBy(item => item.System.MainWorld.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Sector.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.System.Hex)
            .ThenBy(item => item.System.Id)
            .ToArray();
    }
}
