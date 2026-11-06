using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public sealed partial class SceneService
{
    private static int? AutomaticSuffix(string name, string baseName)
    {
        if (name == baseName)
            return 0;
        var prefix = baseName + " ";
        return
            name.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(
                name.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number
            )
            && number > 0
            ? number
            : null;
    }

    private static async Task NormalizeInstanceNames(
        MtuDbContext db,
        int sceneId,
        int characterId,
        int? addedId = null,
        int? removedId = null
    )
    {
        var tokens = await db
            .SceneTokens.Include(t => t.Character)
            .Where(t => t.SceneId == sceneId && t.CharacterId == characterId && t.Id != removedId)
            .OrderBy(t => t.Id)
            .ToArrayAsync();
        if (tokens.Length == 0)
            return;
        var baseName = tokens[0].Character.Name;
        if (tokens.Length == 1)
        {
            if (!tokens[0].HasCustomName)
                tokens[0].Name = baseName;
            return;
        }
        // The second tracker entry establishes 1 and 2, even if the original instance had an older number.
        if (tokens.Length == 2 && addedId.HasValue)
        {
            foreach (var token in tokens)
                if (!token.HasCustomName)
                    token.Name = $"{baseName} {(token.Id == addedId ? 2 : 1)}";
            return;
        }
        var used = tokens
            .Where(t => !t.HasCustomName)
            .Select(t => AutomaticSuffix(t.Name, baseName) ?? 0)
            .Where(n => n > 0)
            .ToHashSet();
        var next = Math.Max(tokens.Length - 1, used.DefaultIfEmpty(0).Max()) + 1;
        foreach (var token in tokens)
            if (!token.HasCustomName && (AutomaticSuffix(token.Name, baseName) ?? 0) == 0)
                token.Name = $"{baseName} {next++}";
    }
}
