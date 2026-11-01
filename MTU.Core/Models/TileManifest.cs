namespace MTU.Core.Models;

/// <summary>Describes the map tiles available on disk so the client can lay them out.</summary>
public sealed class TileManifest
{
    public string BaseUrl { get; set; } = "/tiles";
    public int Columns { get; set; }
    public int Rows { get; set; }

    /// <summary>Width of a sector in parsecs (hex columns are scaled by cos 30°).</summary>
    public double SectorWidth { get; set; }

    /// <summary>Height of a sector in parsecs.</summary>
    public double SectorHeight { get; set; }

    /// <summary>Scales whose tiles are whole sector images.</summary>
    public int[] SectorScales { get; set; } = [];

    /// <summary>Scales whose tiles are subsector images (4x4 per sector).</summary>
    public int[] SubsectorScales { get; set; } = [];

    public List<SectorTiles> Sectors { get; set; } = [];
}

public sealed class SectorTiles
{
    public int Row { get; set; }
    public int Col { get; set; }

    /// <summary>File name without scale folder or extension, e.g. "CE_Spin".</summary>
    public string File { get; set; } = "";

    /// <summary>Scales for which the whole-sector image exists.</summary>
    public int[] SectorScales { get; set; } = [];

    /// <summary>Scales for which all 16 subsector images exist.</summary>
    public int[] SubsectorScales { get; set; } = [];
}
