using Microsoft.EntityFrameworkCore;

namespace MTU.Core.Data;

public class MtuDbContext(DbContextOptions<MtuDbContext> options) : DbContext(options)
{
    public const string FileName = "mtu.dat";

    public static string DefaultDatabasePath { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MTU",
            FileName
        );

    public DbSet<CampaignEntity> Campaigns => Set<CampaignEntity>();
    public DbSet<SceneEntity> Scenes => Set<SceneEntity>();
    public DbSet<SceneTokenEntity> SceneTokens => Set<SceneTokenEntity>();
    public DbSet<NpcEntity> NPCs => Set<NpcEntity>();
    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();
    public DbSet<EquipmentItem> Equipment => Set<EquipmentItem>();
    public DbSet<CharacterEquipmentEntity> CharacterEquipment => Set<CharacterEquipmentEntity>();
    public DbSet<CharacterSkillEntity> CharacterSkills => Set<CharacterSkillEntity>();
    public DbSet<SectorRefreshState> SectorRefreshState => Set<SectorRefreshState>();
    public DbSet<SectorEntity> Sectors => Set<SectorEntity>();
    public DbSet<SectorNameEntity> SectorNames => Set<SectorNameEntity>();
    public DbSet<SubsectorEntity> Subsectors => Set<SubsectorEntity>();
    public DbSet<AllegianceEntity> Allegiances => Set<AllegianceEntity>();
    public DbSet<BorderEntity> Borders => Set<BorderEntity>();
    public DbSet<RouteEntity> Routes => Set<RouteEntity>();
    public DbSet<TradeMarketEntity> TradeMarkets => Set<TradeMarketEntity>();
    public DbSet<TransportMarketEntity> TransportMarkets => Set<TransportMarketEntity>();
    public DbSet<SystemEntity> Systems => Set<SystemEntity>();

    public DbSet<CoverProtection> CoverProtections => Set<CoverProtection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<CoverProtection>()
            .Property(c => c.Name)
            .UseCollation("NOCASE")
            .HasMaxLength(120);
        modelBuilder.Entity<CoverProtection>().HasIndex(c => c.Name).IsUnique();
        modelBuilder
            .Entity<CoverProtection>()
            .ToTable(t =>
            {
                t.HasCheckConstraint("CK_CoverProtections_Protection", "Protection >= 0");
                t.HasCheckConstraint("CK_CoverProtections_Hits", "Hits > 0");
            });
        modelBuilder.Entity<CoverProtection>().HasData(CoverProtectionDefaults.Items);
        modelBuilder.Entity<SceneTokenEntity>().HasKey(t => t.Id);
        modelBuilder.Entity<SceneTokenEntity>().Property(t => t.IsVisible).HasDefaultValue(true);
        modelBuilder
            .Entity<SceneTokenEntity>()
            .HasIndex(t => new
            {
                t.SceneId,
                t.CharacterId,
                t.InstanceNumber,
            })
            .IsUnique();
        modelBuilder
            .Entity<SceneTokenEntity>()
            .HasIndex(t => new { t.SceneId, t.CharacterId })
            .IsUnique()
            .HasFilter("\"InstanceNumber\" IS NULL");
        modelBuilder
            .Entity<SceneTokenEntity>()
            .ToTable(t =>
                t.HasCheckConstraint(
                    "CK_SceneTokens_Position",
                    "(\"Column\" IS NULL AND \"Row\" IS NULL) OR (\"Column\" IS NOT NULL AND \"Row\" IS NOT NULL AND \"Column\" >= 0 AND \"Row\" >= 0)"
                )
            );
        modelBuilder
            .Entity<SceneTokenEntity>()
            .HasOne(t => t.Scene)
            .WithMany()
            .HasForeignKey(t => t.SceneId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<SceneTokenEntity>()
            .HasOne(t => t.Character)
            .WithMany()
            .HasForeignKey(t => t.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<NpcEntity>().HasKey(p => p.CharacterId);
        modelBuilder
            .Entity<NpcEntity>()
            .HasOne(p => p.Character)
            .WithOne()
            .HasForeignKey<NpcEntity>(p => p.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SectorEntity>(e =>
        {
            e.HasIndex(s => new
                {
                    s.Milieu,
                    s.X,
                    s.Y,
                })
                .IsUnique();
            e.HasIndex(s => s.Name);
            e.HasMany(s => s.Names)
                .WithOne(n => n.Sector)
                .HasForeignKey(n => n.SectorId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Subsectors)
                .WithOne(n => n.Sector)
                .HasForeignKey(n => n.SectorId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Allegiances)
                .WithOne(n => n.Sector)
                .HasForeignKey(n => n.SectorId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Borders)
                .WithOne(n => n.Sector)
                .HasForeignKey(n => n.SectorId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Routes)
                .WithOne(n => n.Sector)
                .HasForeignKey(n => n.SectorId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Systems)
                .WithOne(n => n.Sector)
                .HasForeignKey(n => n.SectorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder
            .Entity<CharacterEquipmentEntity>()
            .Property(e => e.Quantity)
            .HasDefaultValue(1);
        modelBuilder.Entity<EquipmentItem>().Ignore(e => e.Summary);
        modelBuilder
            .Entity<EquipmentItem>()
            .HasIndex(e => new
            {
                e.Kind,
                e.Name,
                e.Tl,
            })
            .IsUnique();
        modelBuilder
            .Entity<CharacterEquipmentEntity>()
            .HasKey(e => new { e.CharacterId, e.EquipmentId });
        modelBuilder
            .Entity<CharacterEquipmentEntity>()
            .HasOne(e => e.Character)
            .WithMany()
            .HasForeignKey(e => e.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<CharacterEquipmentEntity>()
            .HasOne(e => e.Equipment)
            .WithMany()
            .HasForeignKey(e => e.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<CharacterSkillEntity>()
            .HasOne(s => s.Character)
            .WithMany()
            .HasForeignKey(s => s.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<CharacterSkillEntity>().Property(s => s.Name).UseCollation("NOCASE");
        modelBuilder
            .Entity<CharacterSkillEntity>()
            .HasIndex(s => new { s.CharacterId, s.Name })
            .IsUnique();
        modelBuilder
            .Entity<CharacterEntity>()
            .HasOne(t => t.Campaign)
            .WithMany()
            .HasForeignKey(t => t.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder
            .Entity<CharacterEntity>()
            .Property(c => c.InjuryTolerance)
            .HasDefaultValue(InjuryTolerance.None);
        modelBuilder.Entity<CharacterEntity>().Property(c => c.Speed).HasDefaultValue(6);
        modelBuilder.Entity<CharacterEntity>().HasIndex(t => new { t.CampaignId, t.Name });
        modelBuilder
            .Entity<CampaignEntity>()
            .HasIndex(c => c.IsActive)
            .IsUnique()
            .HasFilter("\"IsActive\" = 1");
        modelBuilder
            .Entity<TradeMarketEntity>()
            .HasIndex(m => new
            {
                m.SectorId,
                m.Hex,
                m.Mode,
            })
            .IsUnique();
        modelBuilder
            .Entity<TransportMarketEntity>()
            .HasIndex(m => new
            {
                m.SectorId,
                m.Hex,
                m.Kind,
            })
            .IsUnique();
        modelBuilder
            .Entity<SubsectorEntity>()
            .HasIndex(s => new { s.SectorId, s.Letter })
            .IsUnique();
        modelBuilder
            .Entity<AllegianceEntity>()
            .HasIndex(a => new { a.SectorId, a.Code })
            .IsUnique();
        modelBuilder.Entity<SystemEntity>(e =>
        {
            e.HasIndex(s => new { s.SectorId, s.Hex }).IsUnique();
            e.HasIndex(s => s.Name);
        });
    }
}
