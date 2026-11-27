using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

/// <summary>Reads and replaces the Injury Tolerance records of characters, NPCs and animals.</summary>
internal static class InjuryToleranceStore
{
    /// <summary>The records of the matching rows, grouped by character in Sequence order.</summary>
    public static async Task<ILookup<int, InjuryTolerance>> LoadAsync(
        IQueryable<CharacterInjuryToleranceEntity> rows
    ) =>
        (
            await rows.AsNoTracking()
                .OrderBy(t => t.CharacterId)
                .ThenBy(t => t.Sequence)
                .ThenBy(t => t.Id)
                .Select(t => new { t.CharacterId, t.Tolerance })
                .ToArrayAsync()
        ).ToLookup(t => t.CharacterId, t => t.Tolerance);

    /// <summary>Replaces a character's records with the given ones, dropping empty records. The
    /// caller saves the changes. Works for a character that is not saved yet.</summary>
    public static async Task ReplaceAsync(
        MtuDbContext db,
        CharacterEntity character,
        IReadOnlyList<InjuryTolerance>? records
    )
    {
        if (character.Id != 0)
            db.CharacterInjuryTolerances.RemoveRange(
                await db
                    .CharacterInjuryTolerances.Where(t => t.CharacterId == character.Id)
                    .ToArrayAsync()
            );
        var sequence = 0;
        foreach (var record in InjuryToleranceRules.Normalize(records))
            db.CharacterInjuryTolerances.Add(
                new CharacterInjuryToleranceEntity
                {
                    Character = character,
                    Sequence = ++sequence,
                    Tolerance = record,
                }
            );
    }
}
