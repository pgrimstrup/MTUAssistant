using MTU.TravellerMap.Data;
using MTU.TravellerMap.Models;

namespace MTU.TravellerMap;

/// <summary>Downloads sector data into the database, then sector images at every supported scale, for the region of interest.</summary>
public class TileRefreshWorker(TravellerMapClient client, SectorImporter importer)
{
    public const string Milieu = "M1105";
    public const int MinX = -8, MaxX = 7, MinY = -3, MaxY = 4;
    public static readonly TimeSpan DelayBetweenImageCalls = TimeSpan.FromMilliseconds(150);
    public static readonly TimeSpan DelayBetweenDataCalls = TimeSpan.FromMilliseconds(20);

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

        var imageJobs = BuildImageJobs(sectors, scales);
        var total = sectors.Count + imageJobs.Count;
        var completed = 0;
        var failed = 0;
        progress.Report(new(0, total, 0, null, null));

        // Phase 1: system data. A null CurrentScale in progress reports means this phase.
        foreach (var sector in sectors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new(completed, total, failed, sector.Name, null));

            var hasDownloaded = false;
            try
            {
                if (!await importer.ExistsAsync(sector, cancellationToken))
                {
                    var data = await client.GetSectorDataAsync(sector.Name, cancellationToken);
                    hasDownloaded = true;
                    await importer.ImportAsync(sector, data, cancellationToken);
                }
            }
            catch (HttpRequestException)
            {
                failed++;
            }

            completed++;
            progress.Report(new(completed, total, failed, sector.Name, null));
            if (hasDownloaded)
                await Task.Delay(DelayBetweenDataCalls, cancellationToken);
        }

        // Phase 2: images.
        foreach (var (sector, scale, subsector) in imageJobs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new(completed, total, failed, sector.Name, scale, subsector));

            var folder = Path.Combine(rootFolder, ((int)scale).ToString());
            Directory.CreateDirectory(folder);

            bool hasDownloaded = false;
            try
            {
                var destination = Path.Combine(folder, GetFileName(sector, subsector));
                if (!File.Exists(destination))
                {
                    var image = subsector is { } letter
                        ? await client.GetSubsectorImageAsync(sector.Name, letter, scale, cancellationToken)
                        : await client.GetSectorImageAsync(sector.Name, scale, cancellationToken);
                    await File.WriteAllBytesAsync(destination, image, cancellationToken);
                    hasDownloaded = true;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                failed++;
            }

            completed++;
            progress.Report(new(completed, total, failed, sector.Name, scale, subsector));
            if (hasDownloaded)
                await Task.Delay(DelayBetweenImageCalls, cancellationToken);
        }
    }

    /// <summary>One entry per image to fetch, ordered by scale so lower detail levels complete first.</summary>
    private static List<(SectorSummary Sector, SectorImageScale Scale, char? Subsector)> BuildImageJobs(
        List<SectorSummary> sectors, SectorImageScale[] scales)
    {
        var jobs = new List<(SectorSummary, SectorImageScale, char?)>();
        foreach (var scale in scales.Order())
        {
            foreach (var sector in sectors)
            {
                if (scale.UsesSubsectorImages())
                {
                    for (var letter = 'A'; letter <= 'P'; letter++)
                        jobs.Add((sector, scale, letter));
                }
                else
                {
                    jobs.Add((sector, scale, null));
                }
            }
        }
        return jobs;
    }

    /// <summary>Sector tiles are {row}{col}_{abbr}.png; subsector tiles append the letter: {row}{col}_{abbr}_{letter}.png.</summary>
    public static string GetFileName(SectorSummary sector, char? subsector = null)
    {
        var x = (char)('A' + (sector.X - MinX));
        var y = (char)('A' + (sector.Y - MinY));
        var name = string.IsNullOrWhiteSpace(sector.Abbreviation) ? sector.Name : sector.Abbreviation;
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return subsector is { } letter ? $"{y}{x}_{name}_{letter}.png" : $"{y}{x}_{name}.png";
    }
}
