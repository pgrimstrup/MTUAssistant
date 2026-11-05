using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public sealed class CoverProtection
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Protection { get; set; }
    public int Hits { get; set; } = 1;

    public CoverProtection Copy() => (CoverProtection)MemberwiseClone();
}

// Store a snapshot in the encounter so catalog edits/deletions do not rewrite saved combat.
public sealed record CombatCover(int Id, string Name, int Protection, int Hits);

public static class CoverProtectionDefaults
{
    public static IReadOnlyList<CoverProtection> Items { get; } =
    [
        new()
        {
            Id = 1,
            Name = "Vegetation",
            Protection = 2,
            Hits = 1,
        },
        new()
        {
            Id = 2,
            Name = "Tree Trunk",
            Protection = 6,
            Hits = 8,
        },
        new()
        {
            Id = 3,
            Name = "Stone Wall",
            Protection = 8,
            Hits = 24,
        },
        new()
        {
            Id = 4,
            Name = "Civilian Vehicle",
            Protection = 10,
            Hits = 50,
        },
        new()
        {
            Id = 5,
            Name = "Armoured Vehicle",
            Protection = 15,
            Hits = 150,
        },
        new()
        {
            Id = 6,
            Name = "Fortifications",
            Protection = 20,
            Hits = 250,
        },
        new()
        {
            Id = 7,
            Name = "Small Kitchen Appliance",
            Protection = 0,
            Hits = 4,
        },
        new()
        {
            Id = 8,
            Name = "Personal Computer",
            Protection = 0,
            Hits = 6,
        },
        new()
        {
            Id = 9,
            Name = "Wooden Chair",
            Protection = 2,
            Hits = 8,
        },
        new()
        {
            Id = 10,
            Name = "Bar Table",
            Protection = 2,
            Hits = 12,
        },
        new()
        {
            Id = 11,
            Name = "Wooden Door",
            Protection = 1,
            Hits = 6,
        },
        new()
        {
            Id = 12,
            Name = "Reinforced Wooden Door",
            Protection = 2,
            Hits = 8,
        },
        new()
        {
            Id = 13,
            Name = "Wooden Fence",
            Protection = 1,
            Hits = 8,
        },
        new()
        {
            Id = 14,
            Name = "Plastic Door",
            Protection = 2,
            Hits = 5,
        },
        new()
        {
            Id = 15,
            Name = "Brick Wall",
            Protection = 4,
            Hits = 16,
        },
        new()
        {
            Id = 16,
            Name = "Concrete Wall",
            Protection = 8,
            Hits = 24,
        },
        new()
        {
            Id = 17,
            Name = "Reinforced Concrete Wall",
            Protection = 10,
            Hits = 28,
        },
        new()
        {
            Id = 18,
            Name = "Small Boulder",
            Protection = 10,
            Hits = 40,
        },
        new()
        {
            Id = 19,
            Name = "Large Boulder",
            Protection = 10,
            Hits = 60,
        },
        new()
        {
            Id = 20,
            Name = "Steel Hatch",
            Protection = 8,
            Hits = 50,
        },
        new()
        {
            Id = 21,
            Name = "Steel Blast Door",
            Protection = 12,
            Hits = 150,
        },
    ];
}

public sealed class CoverProtectionService(MtuDbContext db)
{
    public async Task<IReadOnlyList<CoverProtection>> ListAsync() =>
        await db.CoverProtections.AsNoTracking().OrderBy(c => c.Name).ToArrayAsync();

    public async Task SaveAsync(CoverProtection item)
    {
        var name = item.Name.Trim();
        if (name.Length is 0 or > 120)
            throw new ArgumentException("Enter a name of up to 120 characters.");
        if (item.Protection < 0 || item.Hits <= 0)
            throw new ArgumentException(
                "Protection must be zero or greater and Hits must be greater than zero."
            );
        if (await db.CoverProtections.AnyAsync(c => c.Id != item.Id && c.Name == name))
            throw new ArgumentException("Cover with this name already exists.");
        var entity =
            item.Id == 0
                ? new CoverProtection()
                : await db.CoverProtections.FindAsync(item.Id)
                    ?? throw new ArgumentException("This cover no longer exists.");
        entity.Name = name;
        entity.Protection = item.Protection;
        entity.Hits = item.Hits;
        if (entity.Id == 0)
            db.CoverProtections.Add(entity);
        await db.SaveChangesAsync();
        item.Id = entity.Id;
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await db.CoverProtections.FindAsync(id);
        if (entity is null)
            return;
        db.CoverProtections.Remove(entity);
        await db.SaveChangesAsync();
    }
}
