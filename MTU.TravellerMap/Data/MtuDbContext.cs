using Microsoft.EntityFrameworkCore;

namespace MTU.TravellerMap.Data;

public class MtuDbContext(DbContextOptions<MtuDbContext> options) : DbContext(options)
{
    public const string FileName = "mtu.dat";

    public static string DefaultDatabasePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MTU", FileName);

    public DbSet<SectorEntity> Sectors => Set<SectorEntity>();
    public DbSet<SectorNameEntity> SectorNames => Set<SectorNameEntity>();
    public DbSet<SubsectorEntity> Subsectors => Set<SubsectorEntity>();
    public DbSet<AllegianceEntity> Allegiances => Set<AllegianceEntity>();
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
            e.HasMany(s => s.Systems).WithOne(n => n.Sector).HasForeignKey(n => n.SectorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubsectorEntity>().HasIndex(s => new { s.SectorId, s.Letter }).IsUnique();
        modelBuilder.Entity<AllegianceEntity>().HasIndex(a => new { a.SectorId, a.Code }).IsUnique();
        modelBuilder.Entity<SystemEntity>(e =>
        {
            e.HasIndex(s => new { s.SectorId, s.Hex }).IsUnique();
            e.HasIndex(s => s.Name);
        });
    }
}
