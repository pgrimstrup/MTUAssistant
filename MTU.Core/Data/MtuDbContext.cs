using Microsoft.EntityFrameworkCore;

namespace MTU.Core.Data;

public class MtuDbContext(DbContextOptions<MtuDbContext> options) : DbContext(options)
{
    public const string FileName = "mtu.dat";

    public static string DefaultDatabasePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MTU", FileName);

    public DbSet<CampaignEntity> Campaigns => Set<CampaignEntity>();
    public DbSet<TravellerEntity> Travellers => Set<TravellerEntity>();
    public DbSet<TravellerSkillEntity> TravellerSkills => Set<TravellerSkillEntity>();
    public DbSet<SectorEntity> Sectors => Set<SectorEntity>();
    public DbSet<SectorNameEntity> SectorNames => Set<SectorNameEntity>();
    public DbSet<SubsectorEntity> Subsectors => Set<SubsectorEntity>();
    public DbSet<AllegianceEntity> Allegiances => Set<AllegianceEntity>();
    public DbSet<BorderEntity> Borders => Set<BorderEntity>();
    public DbSet<RouteEntity> Routes => Set<RouteEntity>();
    public DbSet<TradeMarketEntity> TradeMarkets => Set<TradeMarketEntity>();
    public DbSet<TransportMarketEntity> TransportMarkets => Set<TransportMarketEntity>();
    public DbSet<SystemEntity> Systems => Set<SystemEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SectorEntity>(e =>
        {
            e.HasIndex(s => new { s.Milieu, s.X, s.Y }).IsUnique();
            e.HasIndex(s => s.Name);
            e.HasMany(s => s.Names).WithOne(n => n.Sector).HasForeignKey(n => n.SectorId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Subsectors).WithOne(n => n.Sector).HasForeignKey(n => n.SectorId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Allegiances).WithOne(n => n.Sector).HasForeignKey(n => n.SectorId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Borders).WithOne(n => n.Sector).HasForeignKey(n => n.SectorId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Routes).WithOne(n => n.Sector).HasForeignKey(n => n.SectorId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Systems).WithOne(n => n.Sector).HasForeignKey(n => n.SectorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TravellerSkillEntity>().HasOne(s => s.Traveller).WithMany().HasForeignKey(s => s.TravellerId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TravellerSkillEntity>().Property(s => s.Name).UseCollation("NOCASE");
        modelBuilder.Entity<TravellerSkillEntity>().HasIndex(s => new { s.TravellerId, s.Name }).IsUnique();
        modelBuilder.Entity<TravellerEntity>().HasOne(t => t.Campaign).WithMany().HasForeignKey(t => t.CampaignId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TravellerEntity>().HasIndex(t => new { t.CampaignId, t.TravellerName });
        modelBuilder.Entity<CampaignEntity>().HasIndex(c => c.IsActive).IsUnique().HasFilter("\"IsActive\" = 1");
        modelBuilder.Entity<TradeMarketEntity>().HasIndex(m => new { m.SectorId, m.Hex, m.Mode }).IsUnique();
        modelBuilder.Entity<TransportMarketEntity>().HasIndex(m => new { m.SectorId, m.Hex, m.Kind }).IsUnique();
        modelBuilder.Entity<SubsectorEntity>().HasIndex(s => new { s.SectorId, s.Letter }).IsUnique();
        modelBuilder.Entity<AllegianceEntity>().HasIndex(a => new { a.SectorId, a.Code }).IsUnique();
        modelBuilder.Entity<SystemEntity>(e =>
        {
            e.HasIndex(s => new { s.SectorId, s.Hex }).IsUnique();
            e.HasIndex(s => s.Name);
        });
    }
}
