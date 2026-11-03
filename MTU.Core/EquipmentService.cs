using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public enum EquipmentKind
{
    Armour,
    Weapon,
}

public sealed class EquipmentItem
{
    public int Id { get; set; }
    public EquipmentKind Kind { get; set; }
    public string Name { get; set; } = "";
    public int Protection { get; set; }
    public int? ProtectionL { get; set; }
    public int? ProtectionE { get; set; }
    public int? ProtectionP { get; set; }
    public int? ProtectionK { get; set; }
    public int Tl { get; set; }
    public int Rad { get; set; }
    public decimal Kg { get; set; }
    public decimal Cost { get; set; }
    public string RequiredSkill { get; set; } = "";
    public double Range { get; set; }
    public string Damage { get; set; } = "";
    public string Magazine { get; set; } = "";
    public decimal MagCost { get; set; }
    public string Traits { get; set; } = "";
    public string Notes { get; set; } = "";

    public string FormatProtection()
    {
        var parts = new List<string>
        {
            Protection.ToString("+0;-0;+0", CultureInfo.InvariantCulture),
        };
        foreach (
            var (value, type) in new[]
            {
                (ProtectionL, "Lasers"),
                (ProtectionE, "Energy"),
                (ProtectionP, "Psionics"),
                (ProtectionK, "Kinetic"),
            }
        )
            if (value is { } protection)
                parts.Add(
                    $"{protection.ToString("+0;-0;+0", CultureInfo.InvariantCulture)} vs {type}"
                );
        return string.Join(", ", parts);
    }

    private string SummaryRange() => $"{Range.ToString(CultureInfo.InvariantCulture)}m";

    public EquipmentItem Copy() => (EquipmentItem)MemberwiseClone();

    public string Summary =>
        Kind == EquipmentKind.Armour
            ? $"{Name} ({Protection.ToString(CultureInfo.InvariantCulture)})"
            : $"{Name} ({string.Join(", ", new[] { SummaryRange(), Damage }.Where(s => !string.IsNullOrWhiteSpace(s)))})";
}

public sealed class EquipmentService(MtuDbContext db)
{
    public async Task<IReadOnlyList<EquipmentItem>> ListAsync() =>
        await db
            .Equipment.AsNoTracking()
            .OrderBy(e => e.Kind)
            .ThenBy(e => e.Name)
            .ThenBy(e => e.Tl)
            .ThenBy(e => e.Id)
            .ToArrayAsync();

    public async Task<EquipmentItem> SaveAsync(EquipmentItem item)
    {
        var value = item.Copy();
        value.Name = value.Name.Trim();
        value.RequiredSkill = value.RequiredSkill.Trim();
        value.Damage = value.Damage.Trim();
        value.Magazine = value.Magazine.Trim();
        value.Traits = value.Traits.Trim();
        value.Notes = value.Notes.Trim();
        if (!Enum.IsDefined(value.Kind))
            throw new ArgumentException("Select Armour or Weapon.");
        if (value.Name.Length is 0 or > 120)
            throw new ArgumentException("Enter a name of up to 120 characters.");
        if (
            value.RequiredSkill.Length > 240
            || value.Damage.Length > 120
            || value.Magazine.Length > 120
            || value.Traits.Length > 1000
            || value.Notes.Length > 4000
        )
            throw new ArgumentException("One of the equipment text fields is too long.");
        if (
            !double.IsFinite(value.Range)
            || value.Range < 0
            || value.Protection < 0
            || value.ProtectionL < 0
            || value.ProtectionE < 0
            || value.ProtectionP < 0
            || value.ProtectionK < 0
            || value.Tl < 0
            || value.Rad < 0
            || value.Kg < 0
            || value.Cost < 0
            || value.MagCost < 0
        )
            throw new ArgumentException("Equipment numbers must be zero or greater.");
        if (value.Kind == EquipmentKind.Armour)
        {
            value.Range = 0;
            value.Damage = value.Magazine = value.Traits = "";
            value.MagCost = 0;
        }
        else
        {
            value.Protection = value.Rad = 0;
            value.ProtectionL = value.ProtectionE = value.ProtectionP = value.ProtectionK = null;
            value.RequiredSkill = "";
        }
        if (
            await db.Equipment.AnyAsync(e =>
                e.Id != value.Id && e.Kind == value.Kind && e.Name == value.Name && e.Tl == value.Tl
            )
        )
            throw new ArgumentException(
                "Equipment with this category, name and tech level already exists."
            );
        var entity =
            value.Id == 0
                ? new EquipmentItem()
                : await db.Equipment.SingleAsync(e => e.Id == value.Id);
        if (value.Id != 0 && entity.Kind != value.Kind)
            throw new ArgumentException("An existing item's category cannot be changed.");
        if (value.Id == 0)
            db.Equipment.Add(entity);
        db.Entry(entity).CurrentValues.SetValues(value);
        await db.SaveChangesAsync();
        return entity.Copy();
    }

    public async Task DeleteAsync(int equipmentId)
    {
        var entity = await db.Equipment.SingleAsync(e => e.Id == equipmentId);
        db.Equipment.Remove(entity);
        await db.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<EquipmentItem>> ListForTravellerAsync(
        int campaignId,
        int travellerId
    )
    {
        await RequireTraveller(campaignId, travellerId);
        return await db
            .TravellerEquipment.AsNoTracking()
            .Where(e => e.TravellerId == travellerId)
            .OrderBy(e => e.Equipment.Kind)
            .ThenBy(e => e.Equipment.Name)
            .ThenBy(e => e.Equipment.Tl)
            .ThenBy(e => e.EquipmentId)
            .Select(e => e.Equipment)
            .ToArrayAsync();
    }

    public async Task<IReadOnlyList<TravellerEquipmentDetails>> ListDetailsForTravellerAsync(
        int campaignId,
        int travellerId
    )
    {
        await RequireTraveller(campaignId, travellerId);
        var entries = await db
            .TravellerEquipment.AsNoTracking()
            .Include(e => e.Equipment)
            .Where(e => e.TravellerId == travellerId)
            .OrderBy(e => e.Equipment.Kind)
            .ThenBy(e => e.Equipment.Name)
            .ThenBy(e => e.Equipment.Tl)
            .ThenBy(e => e.EquipmentId)
            .ToArrayAsync();
        return entries
            .Select(e => new TravellerEquipmentDetails
            {
                Equipment = e.Equipment,
                Quantity = e.Quantity,
                Equipped = e.Equipped,
                Scope = e.Scope,
                TargetingSystem = e.TargetingSystem,
                LaserSight = e.LaserSight,
                HolographicSight = e.HolographicSight,
                Fof = e.Fof,
                Phud = e.Phud,
                SmartTracker = e.SmartTracker,
            })
            .ToArray();
    }

    public Task AddToTravellerAsync(int campaignId, int travellerId, int equipmentId) =>
        AddToTravellerAsync(
            campaignId,
            travellerId,
            new TravellerEquipmentDetails { Equipment = new EquipmentItem { Id = equipmentId } }
        );

    public async Task AddToTravellerAsync(
        int campaignId,
        int travellerId,
        TravellerEquipmentDetails details
    )
    {
        await RequireTraveller(campaignId, travellerId);
        var equipment =
            await db
                .Equipment.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == details.Equipment.Id)
            ?? throw new ArgumentException("Select equipment from the catalogue.");
        if (
            await db.TravellerEquipment.AnyAsync(e =>
                e.TravellerId == travellerId && e.EquipmentId == equipment.Id
            )
        )
            throw new ArgumentException(
                "This Traveller already has this equipment. Change its quantity using Options."
            );
        var entity = new TravellerEquipmentEntity
        {
            TravellerId = travellerId,
            EquipmentId = equipment.Id,
        };
        ApplyDetails(entity, equipment, details);
        db.TravellerEquipment.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task SaveTravellerDetailsAsync(
        int campaignId,
        int travellerId,
        TravellerEquipmentDetails details
    )
    {
        await RequireTraveller(campaignId, travellerId);
        var entity =
            await db
                .TravellerEquipment.Include(e => e.Equipment)
                .SingleOrDefaultAsync(e =>
                    e.TravellerId == travellerId && e.EquipmentId == details.Equipment.Id
                )
            ?? throw new ArgumentException("This Traveller no longer has this equipment.");
        ApplyDetails(entity, entity.Equipment, details);
        await db.SaveChangesAsync();
    }

    public async Task SetEquippedAsync(
        int campaignId,
        int travellerId,
        int equipmentId,
        bool equipped
    )
    {
        await RequireTraveller(campaignId, travellerId);
        var entity =
            await db.TravellerEquipment.SingleOrDefaultAsync(e =>
                e.TravellerId == travellerId && e.EquipmentId == equipmentId
            ) ?? throw new ArgumentException("This Traveller no longer has this equipment.");
        entity.Equipped = equipped;
        await db.SaveChangesAsync();
    }

    private static void ApplyDetails(
        TravellerEquipmentEntity entity,
        EquipmentItem equipment,
        TravellerEquipmentDetails details
    )
    {
        if (details.Quantity < 1)
            throw new ArgumentException("Quantity must be at least 1.");
        var supportsAttachments = equipment.Kind == EquipmentKind.Weapon && equipment.Range > 0;
        if (
            !supportsAttachments
            && (
                details.Scope
                || details.TargetingSystem
                || details.LaserSight
                || details.HolographicSight
                || details.Fof
                || details.Phud
                || details.SmartTracker
            )
        )
            throw new ArgumentException(
                "Attachments are only available for non-melee weapons (range greater than zero)."
            );
        entity.Quantity = details.Quantity;
        entity.Equipped = details.Equipped;
        entity.Scope = details.Scope;
        entity.TargetingSystem = details.TargetingSystem;
        entity.LaserSight = details.LaserSight;
        entity.HolographicSight = details.HolographicSight;
        entity.Fof = details.Fof;
        entity.Phud = details.Phud;
        entity.SmartTracker = details.SmartTracker;
    }

    public async Task RemoveFromTravellerAsync(int campaignId, int travellerId, int equipmentId)
    {
        await RequireTraveller(campaignId, travellerId);
        var link = await db.TravellerEquipment.SingleAsync(e =>
            e.TravellerId == travellerId && e.EquipmentId == equipmentId
        );
        db.TravellerEquipment.Remove(link);
        await db.SaveChangesAsync();
    }

    private async Task RequireTraveller(int campaignId, int travellerId)
    {
        if (!await db.Travellers.AnyAsync(t => t.Id == travellerId && t.CampaignId == campaignId))
            throw new ArgumentException("This Traveller is not in the current campaign.");
    }
}
