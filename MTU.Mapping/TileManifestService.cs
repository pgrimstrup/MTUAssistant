using System.Text.RegularExpressions;
using MTU.Core;
using MTU.Core.Models;

namespace MTU.Mapping;

public sealed partial class TileManifestService(MappingOptions options) : ITileManifestService
{
    private static readonly int[] AllScales = [8, 16, 32, 64, 128];
    private const int SubsectorsPerSector = 16;

    [GeneratedRegex(@"^([A-Z])([A-Z])_(.+?)(?:_([A-P]))?\.png$", RegexOptions.IgnoreCase)]
    private static partial Regex TileName();

    public TileManifest GetManifest()
    {
        // key: "row,col" -> (file, scale -> count of files)
        var sectors =
            new Dictionary<
                (int Row, int Col),
                (string File, Dictionary<int, int> Whole, Dictionary<int, int> Sub)
            >();

        foreach (var scale in AllScales)
        {
            var folder = Path.Combine(options.TileRoot, scale.ToString());
            if (!Directory.Exists(folder))
                continue;

            foreach (var path in Directory.EnumerateFiles(folder, "*.png"))
            {
                var m = TileName().Match(Path.GetFileName(path));
                if (!m.Success)
                    continue;

                var row = char.ToUpperInvariant(m.Groups[1].Value[0]) - 'A';
                var col = char.ToUpperInvariant(m.Groups[2].Value[0]) - 'A';
                var isSub = m.Groups[4].Success;
                var file = isSub
                    ? $"{m.Groups[1].Value}{m.Groups[2].Value}_{m.Groups[3].Value}"
                    : Path.GetFileNameWithoutExtension(path);

                if (!sectors.TryGetValue((row, col), out var entry))
                    sectors[(row, col)] = entry = (file, [], []);

                var counts = isSub ? entry.Sub : entry.Whole;
                counts[scale] = counts.GetValueOrDefault(scale) + 1;
            }
        }

        var manifest = new TileManifest
        {
            SectorWidth = 32 * Math.Cos(Math.PI / 6),
            SectorHeight = 40,
            SectorScales = AllScales.Where(s => s < 64).ToArray(),
            SubsectorScales = AllScales.Where(s => s >= 64).ToArray(),
        };

        foreach (var ((row, col), e) in sectors.OrderBy(k => k.Key.Row).ThenBy(k => k.Key.Col))
        {
            manifest.Rows = Math.Max(manifest.Rows, row + 1);
            manifest.Columns = Math.Max(manifest.Columns, col + 1);
            manifest.Sectors.Add(
                new SectorTiles
                {
                    Row = row,
                    Col = col,
                    File = e.File,
                    SectorScales = e.Whole.Keys.Order().ToArray(),
                    SubsectorScales = e
                        .Sub.Where(kv => kv.Value >= SubsectorsPerSector)
                        .Select(kv => kv.Key)
                        .Order()
                        .ToArray(),
                }
            );
        }

        return manifest;
    }
}
