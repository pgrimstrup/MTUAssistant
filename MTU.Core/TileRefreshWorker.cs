using MTU.Core.Data;

namespace MTU.Core;

/// <summary>Refreshes sector systems and metadata together, committing each sector atomically.</summary>
public class TileRefreshWorker(ITravellerMapClient client, SectorImporter importer)
{
    public const string Milieu = "M1105";
    public const int MinX = -8,
        MaxX = 7,
        MinY = -3,
        MaxY = 4;
    public static string DefaultRootFolder { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MTU"
        );

    public async Task RunAsync(
        IProgress<TileRefreshProgress> progress,
        CancellationToken cancellationToken = default
    )
    {
        var sectors = (await client.GetSectorsAsync(cancellationToken))
            .Where(s =>
                s.Milieu == Milieu && s.X is >= MinX and <= MaxX && s.Y is >= MinY and <= MaxY
            )
            .OrderBy(s => s.Y)
            .ThenBy(s => s.X)
            .ToList();
        var completed = 0;
        var failed = 0;
        progress.Report(new(0, sectors.Count, 0, null));
        foreach (var sector in sectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new(completed, sectors.Count, failed, sector.Name));
            try
            {
                var data = await client.GetSectorDataAsync(sector.Name, cancellationToken);
                var metadata = await client.GetMetadataAsync(
                    sector.Name,
                    Milieu,
                    cancellationToken
                );
                await importer.ImportAsync(sector, data, metadata, cancellationToken);
            }
            catch (Exception ex)
                when (ex is HttpRequestException or IOException or System.Text.Json.JsonException)
            {
                failed++;
            }
            progress.Report(new(++completed, sectors.Count, failed, sector.Name));
        }
    }
}
