using MTU.Core.Models;

namespace MTU.Core;

public interface ITileManifestService
{
    /// <summary>Builds a manifest by scanning the tile folders on disk.</summary>
    TileManifest GetManifest();
}
