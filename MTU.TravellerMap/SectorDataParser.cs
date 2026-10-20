using System.Globalization;
using System.Text.RegularExpressions;
using MTU.TravellerMap.Models;

namespace MTU.TravellerMap;

/// <summary>Parses the Second Survey column-formatted text returned by /data/{sector}.</summary>
public static partial class SectorDataParser
{
    [GeneratedRegex(@"^#\s*Subsector\s+([A-P]):\s*(.*)$")]
    private static partial Regex SubsectorRegex();

    [GeneratedRegex(@"^#\s*Alleg:\s*(\S+):\s*""?(.*?)""?\s*$")]
    private static partial Regex AllegianceRegex();

    [GeneratedRegex(@"^#\s*(Name|Abbreviation|Milieu|Credits|Source):\s*(.*)$")]
    private static partial Regex KeyValueRegex();

    [GeneratedRegex(@"^#\s*(-?\d+),(-?\d+)\s*$")]
    private static partial Regex CoordinatesRegex();

    [GeneratedRegex(@"^(.*?)\s*\((\w+)\)$")]
    private static partial Regex LangNameRegex();

    public static SectorData Parse(string text)
    {
        var sector = new SectorData();
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r'));

        string? header = null;
        List<(int Start, int Length)>? columns = null;
        var sawTitle = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith('#'))
            {
                ParseComment(sector, line, ref sawTitle);
                continue;
            }

            if (header is null)
            {
                header = line;
                continue;
            }

            if (columns is null)
            {
                columns = GetColumns(line);
                continue;
            }

            var values = ReadColumns(line, columns);
            var map = columns.Select((_, i) => (Key: ReadColumns(header, columns)[i], Value: values[i]))
                             .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

            sector.Systems.Add(new StarSystem
            {
                Hex = Get(map, "Hex"),
                Name = Get(map, "Name"),
                Uwp = Get(map, "UWP"),
                Remarks = Get(map, "Remarks"),
                Ix = Get(map, "{Ix}"),
                Ex = Get(map, "(Ex)"),
                Cx = Get(map, "[Cx]"),
                N = Dash(Get(map, "N")),
                B = Dash(Get(map, "B")),
                Z = Dash(Get(map, "Z")),
                Pbg = Get(map, "PBG"),
                W = Get(map, "W"),
                A = Get(map, "A"),
                Stellar = Get(map, "Stellar")
            });
        }

        if (string.IsNullOrEmpty(sector.Name) && sector.Names.Count > 0)
            sector.Name = sector.Names[0].Text;

        return sector;
    }

    private static void ParseComment(SectorData sector, string line, ref bool sawTitle)
    {
        Match m;
        if ((m = SubsectorRegex().Match(line)).Success)
        {
            sector.Subsectors.Add(new Subsector { Letter = m.Groups[1].Value, Name = m.Groups[2].Value.Trim() });
        }
        else if ((m = AllegianceRegex().Match(line)).Success)
        {
            sector.Allegiances.Add(new Allegiance { Code = m.Groups[1].Value, Name = m.Groups[2].Value });
        }
        else if ((m = CoordinatesRegex().Match(line)).Success)
        {
            sector.X = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            sector.Y = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
        }
        else if ((m = KeyValueRegex().Match(line)).Success)
        {
            var value = m.Groups[2].Value.Trim();
            switch (m.Groups[1].Value)
            {
                case "Name":
                    var lang = LangNameRegex().Match(value);
                    sector.Names.Add(lang.Success
                        ? new SectorName { Text = lang.Groups[1].Value, Lang = lang.Groups[2].Value }
                        : new SectorName { Text = value });
                    if (string.IsNullOrEmpty(sector.Name))
                        sector.Name = sector.Names[^1].Text;
                    break;
                case "Abbreviation": sector.Abbreviation = value; break;
                case "Milieu": sector.Milieu = value; break;
                case "Credits": sector.Credits = value; break;
                case "Source": sector.Source = value; break;
            }
        }
    }

    // The dashed line under the header defines each column's start and width.
    private static List<(int Start, int Length)> GetColumns(string dashes)
    {
        var columns = new List<(int, int)>();
        var i = 0;
        while (i < dashes.Length)
        {
            if (dashes[i] != '-') { i++; continue; }
            var start = i;
            while (i < dashes.Length && dashes[i] == '-') i++;
            columns.Add((start, i - start));
        }
        return columns;
    }

    private static string[] ReadColumns(string line, List<(int Start, int Length)> columns)
    {
        var result = new string[columns.Count];
        for (var i = 0; i < columns.Count; i++)
        {
            var (start, length) = columns[i];
            // The last column runs to the end of the line.
            var end = i == columns.Count - 1 ? line.Length : Math.Min(start + length, line.Length);
            result[i] = start >= line.Length ? string.Empty : line[start..end].Trim();
        }
        return result;
    }

    private static string Get(Dictionary<string, string> map, string key) =>
        map.TryGetValue(key, out var v) ? v : string.Empty;

    private static string Dash(string value) => value == "-" ? string.Empty : value;
}
