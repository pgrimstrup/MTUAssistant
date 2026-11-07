using Microsoft.EntityFrameworkCore;
using MTU.Core.Models;

namespace MTU.Core.Data;

/// <summary>Stores parsed sector data in the database, replacing any previous import of the same sector.</summary>
public class SectorImporter(MtuDbContext db)
{
    /// <summary>True if the sector has already been imported. Delete the sector row to force a refresh.</summary>
    public Task<bool> ExistsAsync(
        SectorSummary summary,
        CancellationToken cancellationToken = default
    ) =>
        db.Sectors.AnyAsync(
            s => s.Milieu == summary.Milieu && s.X == summary.X && s.Y == summary.Y,
            cancellationToken
        );

    public async Task ImportAsync(
        SectorSummary summary,
        SectorData data,
        SectorMetadata metadata,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var sector = await db.Sectors.FirstOrDefaultAsync(
            s => s.Milieu == summary.Milieu && s.X == summary.X && s.Y == summary.Y,
            cancellationToken
        );

        if (sector is null)
        {
            sector = new SectorEntity
            {
                Milieu = summary.Milieu,
                X = summary.X,
                Y = summary.Y,
            };
            db.Sectors.Add(sector);
        }
        else
        {
            // Keep the sector row (and its Id) stable; replace only the children.
            await db
                .SectorNames.Where(n => n.SectorId == sector.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db
                .Subsectors.Where(n => n.SectorId == sector.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db
                .Allegiances.Where(n => n.SectorId == sector.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db
                .Systems.Where(n => n.SectorId == sector.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db
                .Borders.Where(n => n.SectorId == sector.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await db
                .Routes.Where(n => n.SectorId == sector.Id)
                .ExecuteDeleteAsync(cancellationToken);
        }

        sector.Name = summary.Name;
        sector.Abbreviation = summary.Abbreviation ?? data.Abbreviation;
        sector.Tags = summary.Tags;
        sector.Credits = data.Credits;
        sector.Source = data.Source;
        sector.Stylesheet = metadata.Stylesheet;
        sector.DataImportedUtc = DateTime.UtcNow;

        // Prefer the names from the sector list; fall back to those in the data file.
        var names = summary.Names.Count > 0 ? summary.Names : data.Names;
        sector.Names = names
            .Select(n => new SectorNameEntity
            {
                Text = n.Text,
                Lang = n.Lang,
                Source = n.Source,
            })
            .ToList();

        sector.Subsectors =
            metadata.Subsectors.Count > 0
                ? metadata
                    .Subsectors.DistinctBy(s => s.Index)
                    .Select(s => new SubsectorEntity { Letter = s.Index, Name = s.Name })
                    .ToList()
                : data
                    .Subsectors.DistinctBy(s => s.Letter)
                    .Select(s => new SubsectorEntity { Letter = s.Letter, Name = s.Name })
                    .ToList();
        // Metadata enriches the allegiance list; retain codes only present in sector data.
        sector.Allegiances = metadata
            .Allegiances.Select(a => new AllegianceEntity
            {
                Code = a.Code,
                Name = a.Name,
                Base = a.Base,
                ShortName = a.ShortName,
            })
            .Concat(
                data.Allegiances.Select(a => new AllegianceEntity { Code = a.Code, Name = a.Name })
            )
            .Where(a => !string.IsNullOrWhiteSpace(a.Code))
            .DistinctBy(a => a.Code)
            .ToList();
        sector.Borders = metadata
            .Borders.Select(b => new BorderEntity
            {
                Path = b.Path,
                Allegiance = b.Allegiance,
                Color =
                    b.Color ?? MetadataStyles.Color(metadata.Stylesheet, "border", b.Allegiance),
                Style = b.Style,
                RawJson = System.Text.Json.JsonSerializer.Serialize(b),
            })
            .ToList();
        sector.Routes = metadata
            .Routes.Select(r => new RouteEntity
            {
                Start = r.Start,
                End = r.End,
                StartOffsetX = r.StartOffsetX,
                StartOffsetY = r.StartOffsetY,
                EndOffsetX = r.EndOffsetX,
                EndOffsetY = r.EndOffsetY,
                Allegiance = r.Allegiance,
                Type = r.Type,
                Color =
                    r.Color
                    ?? MetadataStyles.Color(metadata.Stylesheet, "route", r.Allegiance, r.Type),
                Style = r.Style,
                Width = r.Width,
                RawJson = System.Text.Json.JsonSerializer.Serialize(r),
            })
            .ToList();

        sector.Systems = data
            .Systems.DistinctBy(s => s.Hex)
            .Select(s =>
            {
                var (relX, relY) = GetRelativeCoordinates(summary.X, summary.Y, s.Hex);
                return new SystemEntity
                {
                    SubsectorLetter = GetSubsectorLetter(s.Hex),
                    Hex = s.Hex,
                    RelX = relX,
                    RelY = relY,
                    Name = s.Name,
                    Uwp = s.Uwp,
                    Remarks = s.Remarks,
                    Ix = s.Ix,
                    Ex = s.Ex,
                    Cx = s.Cx,
                    N = s.N,
                    B = s.B,
                    Z = s.Z,
                    Pbg = s.Pbg,
                    W = s.W,
                    A = s.A,
                    Stellar = s.Stellar,
                };
            })
            .ToList();

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // The context is long-lived (scoped); don't keep thousands of tracked entities around.
        db.ChangeTracker.Clear();
    }

    /// <summary>
    /// World-space coordinates relative to Reference (Core 0140, which is 0,0).
    /// Sectors are 32x40 hexes; Core's hex 0140 is column 1, row 40.
    /// </summary>
    public static (int RelX, int RelY) GetRelativeCoordinates(int sectorX, int sectorY, string hex)
    {
        if (
            hex.Length != 4
            || !int.TryParse(hex[..2], out var col)
            || !int.TryParse(hex[2..], out var row)
        )
            throw new FormatException($"Invalid hex '{hex}'.");

        return (sectorX * 32 + (col - 1), sectorY * 40 + (row - 40));
    }

    /// <summary>Sectors are 32x40 hexes split 4x4 into subsectors of 8x10, lettered A-P row by row.</summary>
    public static string GetSubsectorLetter(string hex)
    {
        if (
            hex.Length != 4
            || !int.TryParse(hex[..2], out var col)
            || !int.TryParse(hex[2..], out var row)
        )
            return string.Empty;

        var index = (row - 1) / 10 * 4 + (col - 1) / 8;
        return index is >= 0 and < 16 ? ((char)('A' + index)).ToString() : string.Empty;
    }
}
