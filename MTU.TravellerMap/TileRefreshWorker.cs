using MTU.TravellerMap.Models;

namespace MTU.TravellerMap;

/// <summary>Downloads sector images for the region of interest at every supported scale.</summary>
public class TileRefreshWorker(TravellerMapClient client)
{
    public const string Milieu = "M1105";
    public const int MinX = -8, MaxX = 7, MinY = -3, MaxY = 4;
    public static readonly TimeSpan DelayBetweenCalls = TimeSpan.FromMilliseconds(150);

    public static string DefaultRootFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MTU");

    public async Task RunAsync(IProgress<TileRefreshProgress> progress, string? rootFolder = null, CancellationToken cancellationToken = default)
    {
        rootFolder ??= DefaultRootFolder;
        var scales = Enum.GetValues<SectorImageScale>();

        var sectors = (await client.GetSectorsAsync(cancellationToken))
            .Where(s => s.Milieu == Milieu && s.X is >= MinX and <= MaxX && s.Y is >= MinY and <= MaxY)
            .OrderBy(s => s.Y).ThenBy(s => s.X)
            .ToList();

        var total = sectors.Count * scales.Length;
        var completed = 0;
        var failed = 0;
        progress.Report(new(0, total, 0, null, null));

        foreach (var scale in scales)
        {
            var folder = Path.Combine(rootFolder, ((int)scale).ToString());
            Directory.CreateDirectory(folder);

            foreach (var sector in sectors)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress.Report(new(completed, total, failed, sector.Name, scale));

                bool hasDownloaded = false;
                try
                {
                    var destination = Path.Combine(folder, GetFileName(sector));
                    if (!File.Exists(destination))
                    {
                        var image = await client.GetSectorImageAsync(sector.Name, scale, cancellationToken);
                        await File.WriteAllBytesAsync(destination, image, cancellationToken);
                        hasDownloaded = true;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException)
                {
                    failed++;
                }

                completed++;
                progress.Report(new(completed, total, failed, sector.Name, scale));
                if(hasDownloaded)
                    await Task.Delay(DelayBetweenCalls, cancellationToken);
            }
        }
    }

    public static string GetFileName(SectorSummary sector)
    {
        var x = (char)('A' + (sector.X - MinX));
        var y = (char)('A' + (sector.Y - MinY));
        var name = string.IsNullOrWhiteSpace(sector.Abbreviation) ? sector.Name : sector.Abbreviation;
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return $"{y}{x}_{name}.png";
    }
}
