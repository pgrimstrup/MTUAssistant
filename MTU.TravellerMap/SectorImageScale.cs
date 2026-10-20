namespace MTU.TravellerMap;

/// <summary>Pixels per parsec supported for map images. Powers of two render best on Traveller Map.</summary>
public enum SectorImageScale
{
    Scale8 = 8,
    Scale16 = 16,
    Scale32 = 32,
    Scale64 = 64,
    Scale128 = 128
}

public static class SectorImageScaleExtensions
{
    public const int FirstSubsectorScale = 64;

    /// <summary>At these scales whole sectors are too large, so the 16 subsectors are downloaded individually.</summary>
    public static bool UsesSubsectorImages(this SectorImageScale scale) => (int)scale >= FirstSubsectorScale;
}
