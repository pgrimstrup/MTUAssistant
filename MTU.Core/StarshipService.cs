using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

/// <summary>One system of a starship. Displacement is in tons and Cost in MCr; what Rating means
/// depends on the Type (see <see cref="StarshipRules.RatingHint"/>).</summary>
public sealed record StarshipSystem(
    int Id,
    StarshipSystemType Type,
    string Description,
    decimal Displacement,
    decimal Cost,
    decimal PowerRequirement,
    int Rating
);

/// <summary>A starship available to the Travellers. Systems are listed by Type. A Prototype pays
/// full price for its systems; a standard design pays 90%.</summary>
public sealed record Starship(
    int Id,
    int CampaignId,
    string Name,
    int TechLevel,
    int Displacement,
    int DamageTaken,
    bool Prototype,
    string Crew,
    bool Equipped,
    IReadOnlyList<StarshipSystem> Systems,
    // The ship's class or role, such as "Free Trader" or "Type-S Scout"; shown as Type.
    string Classification = ""
)
{
    /// <summary>Hull Points, drive ratings, power and cargo, worked out from the systems.</summary>
    public StarshipPerformance Performance => StarshipRules.Performance(Systems);

    /// <summary>Hull Points left after the Damage Taken, never below 0.</summary>
    public int HullRemaining => Math.Max(0, Performance.HullPoints - DamageTaken);

    /// <summary>Purchase Cost in MCr, worked out from the systems' costs.</summary>
    public decimal PurchaseCost => StarshipRules.PurchaseCost(Systems, Prototype);

    /// <summary>Maintenance Cost in credits per month.</summary>
    public decimal MaintenanceCost => StarshipRules.MaintenanceCost(PurchaseCost);
}

/// <summary>Stores a campaign's starships and their systems, and which one the party is using.</summary>
public sealed class StarshipService(MtuDbContext db)
{
    public async Task<IReadOnlyList<Starship>> ListAsync(int campaignId)
    {
        var ships = await db
            .Starships.AsNoTracking()
            .Where(s => s.CampaignId == campaignId)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToArrayAsync();
        var systems = (
            await db
                .StarshipSystems.AsNoTracking()
                .Where(s => s.Starship.CampaignId == campaignId)
                .ToArrayAsync()
        ).ToLookup(s => s.StarshipId);
        return ships.Select(s => ToModel(s, systems[s.Id])).ToArray();
    }

    /// <summary>The ship the party is using now, or null when none is equipped. Galaxy Map travel
    /// reads the J-Drive and M-Drive ratings from its <see cref="Starship.Performance"/>.</summary>
    public async Task<Starship?> EquippedAsync(int campaignId)
    {
        var ship = await db
            .Starships.AsNoTracking()
            .SingleOrDefaultAsync(s => s.CampaignId == campaignId && s.Equipped);
        return ship is null ? null : await LoadAsync(ship);
    }

    public async Task<Starship?> GetAsync(int campaignId, int id)
    {
        var ship = await db
            .Starships.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id && s.CampaignId == campaignId);
        return ship is null ? null : await LoadAsync(ship);
    }

    /// <summary>Adds a ship (Id 0) or updates one, replacing all its systems. Equipped isn't
    /// changed here: use <see cref="SetEquippedAsync"/>.</summary>
    public async Task<Starship> SaveAsync(Starship ship)
    {
        var name = ship.Name?.Trim() ?? "";
        if (name.Length is 0 or > 120)
            throw new ArgumentException("Enter a starship name of up to 120 characters.");
        var classification = ship.Classification?.Trim() ?? "";
        if (classification.Length > 120)
            throw new ArgumentException("Type must be at most 120 characters.");
        if (ship.TechLevel is < StarshipRules.MinTechLevel or > StarshipRules.MaxTechLevel)
            throw new ArgumentException(
                $"Tech Level must be from {StarshipRules.MinTechLevel} to {StarshipRules.MaxTechLevel}."
            );
        if (ship.Displacement is < StarshipRules.MinDisplacement or > StarshipRules.MaxDisplacement)
            throw new ArgumentException(
                $"Displacement must be from {StarshipRules.MinDisplacement:N0} to {StarshipRules.MaxDisplacement:N0} tons."
            );
        if (ship.DamageTaken is < 0 or > StarshipRules.MaxRating)
            throw new ArgumentException("Damage Taken must be zero or greater.");
        var crew = ship.Crew?.Trim() ?? "";
        if (crew.Length > 1000)
            throw new ArgumentException("Crew must be at most 1000 characters.");
        var systems = ship.Systems ?? [];
        if (systems.Count > StarshipRules.MaxSystems)
            throw new ArgumentException(
                $"A starship can have at most {StarshipRules.MaxSystems} systems."
            );
        foreach (var system in systems)
        {
            var label = Enum.IsDefined(system.Type)
                ? StarshipRules.Label(system.Type)
                : throw new ArgumentException("Choose a type for every system.");
            if ((system.Description?.Trim().Length ?? 0) > 200)
                throw new ArgumentException(
                    $"{label}: the description must be at most 200 characters."
                );
            if (
                system.Displacement is < 0 or > StarshipRules.MaxDisplacement
                || !InRange(system.Cost)
                || system.PowerRequirement is < 0 or > StarshipRules.MaxRating
                || system.Rating is < 0 or > StarshipRules.MaxRating
            )
                throw new ArgumentException(
                    $"{label}: tons, cost, power and rating must be zero or greater."
                );
        }
        if (!await db.Campaigns.AnyAsync(c => c.Id == ship.CampaignId))
            throw new ArgumentException("The selected campaign no longer exists.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        StarshipEntity entity;
        if (ship.Id == 0)
        {
            entity = new StarshipEntity { CampaignId = ship.CampaignId };
            db.Starships.Add(entity);
        }
        else
        {
            entity =
                await db.Starships.SingleOrDefaultAsync(s =>
                    s.Id == ship.Id && s.CampaignId == ship.CampaignId
                ) ?? throw new ArgumentException("This starship is not in the current campaign.");
            await db.StarshipSystems.Where(s => s.StarshipId == entity.Id).ExecuteDeleteAsync();
        }
        entity.Name = name;
        entity.Classification = classification;
        entity.TechLevel = ship.TechLevel;
        entity.Displacement = ship.Displacement;
        entity.DamageTaken = ship.DamageTaken;
        entity.Prototype = ship.Prototype;
        // The costs are worked out from the systems; they are stored too, so the table always
        // holds what the app shows.
        entity.PurchaseCost = ship.PurchaseCost;
        entity.MaintenanceCost = ship.MaintenanceCost;
        entity.Crew = crew;
        await db.SaveChangesAsync();
        // Systems keep the order they were given in within each type.
        var sequence = 0;
        foreach (var system in systems.OrderBy(s => s.Type))
            db.StarshipSystems.Add(
                new StarshipSystemEntity
                {
                    StarshipId = entity.Id,
                    Type = system.Type,
                    Sequence = ++sequence,
                    Description = system.Description?.Trim() ?? "",
                    Displacement = system.Displacement,
                    Cost = system.Cost,
                    PowerRequirement = system.PowerRequirement,
                    Rating = system.Rating,
                }
            );
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        db.ChangeTracker.Clear();
        return (await GetAsync(entity.CampaignId, entity.Id))!;
    }

    /// <summary>Makes this ship the one the party is using, unequipping any other, or unequips it.</summary>
    public async Task SetEquippedAsync(int campaignId, int id, bool equipped)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await db.Starships.AnyAsync(s => s.Id == id && s.CampaignId == campaignId))
            throw new ArgumentException("This starship is not in the current campaign.");
        // Unequip first: only one ship per campaign may be equipped at any moment.
        await db
            .Starships.Where(s =>
                s.CampaignId == campaignId && s.Equipped && (equipped ? s.Id != id : s.Id == id)
            )
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Equipped, false));
        if (equipped)
            await db
                .Starships.Where(s => s.Id == id && s.CampaignId == campaignId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Equipped, true));
        await transaction.CommitAsync();
    }

    public async Task DeleteAsync(int campaignId, int id)
    {
        if (
            await db
                .Starships.Where(s => s.Id == id && s.CampaignId == campaignId)
                .ExecuteDeleteAsync() == 0
        )
            throw new ArgumentException("This starship is not in the current campaign.");
    }

    private static bool InRange(decimal value) => value is >= 0 and <= StarshipRules.MaxCost;

    private async Task<Starship> LoadAsync(StarshipEntity ship) =>
        ToModel(
            ship,
            await db
                .StarshipSystems.AsNoTracking()
                .Where(s => s.StarshipId == ship.Id)
                .ToArrayAsync()
        );

    private static Starship ToModel(StarshipEntity s, IEnumerable<StarshipSystemEntity> systems) =>
        new(
            s.Id,
            s.CampaignId,
            s.Name,
            s.TechLevel,
            s.Displacement,
            s.DamageTaken,
            s.Prototype,
            s.Crew,
            s.Equipped,
            systems
                .OrderBy(x => x.Type)
                .ThenBy(x => x.Sequence)
                .ThenBy(x => x.Id)
                .Select(x => new StarshipSystem(
                    x.Id,
                    x.Type,
                    x.Description,
                    x.Displacement,
                    x.Cost,
                    x.PowerRequirement,
                    x.Rating
                ))
                .ToArray(),
            s.Classification
        );
}
