using Microsoft.EntityFrameworkCore;
using MTU.Core.Data;

namespace MTU.Core;

public sealed record Campaign(int Id, string Name, int Day, int Year, int? LocationSectorId, string? LocationHex, bool IsActive);
public sealed class CampaignService(MtuDbContext db)
{
    public event Action<Campaign?>? Changed;
    private async Task PublishAsync() => Changed?.Invoke((await ListAsync()).FirstOrDefault(c => c.IsActive));
    private static Campaign ToCampaign(CampaignEntity e) => new(e.Id, e.Name, e.Day, e.Year, e.LocationSectorId, e.LocationHex, e.IsActive);
    public async Task<IReadOnlyList<Campaign>> ListAsync() => (await db.Campaigns.AsNoTracking().OrderBy(c => c.Name).ThenBy(c => c.Id).ToListAsync()).Select(ToCampaign).ToArray();

    public async Task SaveAsync(Campaign campaign)
    {
        var name = campaign.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 120) throw new ArgumentException("Enter a campaign name of up to 120 characters.");
        if (campaign.Day is < 1 or > 365) throw new ArgumentException("Day must be between 1 and 365.");
        if (campaign.Year < 1) throw new ArgumentException("Year must be at least 1.");
        if ((campaign.LocationSectorId is null) != (campaign.LocationHex is null)) throw new ArgumentException("Select a valid current location.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var entity = campaign.Id == 0 ? new CampaignEntity() : await db.Campaigns.SingleAsync(c => c.Id == campaign.Id);
        if (campaign.Id == 0)
        {
            foreach (var active in await db.Campaigns.Where(c => c.IsActive).ToListAsync()) active.IsActive = false;
            await db.SaveChangesAsync();
            entity.IsActive = true;
            db.Campaigns.Add(entity);
        }
        entity.Name = name;
        entity.Day = campaign.Day;
        entity.Year = campaign.Year;
        entity.LocationSectorId = campaign.LocationSectorId;
        entity.LocationHex = campaign.LocationHex;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        await PublishAsync();
    }

    public async Task AdjustDateAsync(int id, int days)
    {
        var campaign = await db.Campaigns.SingleAsync(c => c.Id == id);
        var absoluteDay = ((long)campaign.Year - 1) * 365 + campaign.Day - 1 + days;
        if (absoluteDay < 0 || absoluteDay >= (long)int.MaxValue * 365)
            throw new ArgumentException("The resulting campaign date is outside the supported range.");
        campaign.Year = (int)(absoluteDay / 365) + 1;
        campaign.Day = (int)(absoluteDay % 365) + 1;
        await db.SaveChangesAsync();
        await PublishAsync();
    }

    public async Task ActivateAsync(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var target = await db.Campaigns.SingleAsync(c => c.Id == id);
        foreach (var active in await db.Campaigns.Where(c => c.IsActive && c.Id != id).ToListAsync()) active.IsActive = false;
        await db.SaveChangesAsync();
        target.IsActive = true;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        await PublishAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var target = await db.Campaigns.SingleAsync(c => c.Id == id);
        var wasActive = target.IsActive;
        db.Campaigns.Remove(target);
        await db.SaveChangesAsync();
        if (wasActive)
        {
            var next = await db.Campaigns.OrderBy(c => c.Name).ThenBy(c => c.Id).FirstOrDefaultAsync();
            if (next is not null) { next.IsActive = true; await db.SaveChangesAsync(); }
        }
        await transaction.CommitAsync();
        await PublishAsync();
    }
}
