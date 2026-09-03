using BimMep.Services.ProjectManagement.Entities;
using Microsoft.EntityFrameworkCore;

namespace BimMep.Services.ProjectManagement;

/// <summary>
/// Contexte EF Core mappant le schema PostgreSQL documente (docs 04-base-de-donnees.md). Utilise
/// Npgsql + NetTopologySuite pour les colonnes géométriques PostGIS (placement, boundary, bbox).
/// </summary>
public sealed class BimMepDbContext : DbContext
{
    public BimMepDbContext(DbContextOptions<BimMepDbContext> options) : base(options) { }

    public DbSet<OrganizationEntity> Organizations => Set<OrganizationEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();
    public DbSet<LevelEntity> Levels => Set<LevelEntity>();
    public DbSet<FamilyEntity> Families => Set<FamilyEntity>();
    public DbSet<FamilyTypeEntity> FamilyTypes => Set<FamilyTypeEntity>();
    public DbSet<BimElementEntity> BimElements => Set<BimElementEntity>();
    public DbSet<MepNetworkEntity> MepNetworks => Set<MepNetworkEntity>();
    public DbSet<MepConnectorEntity> MepConnectors => Set<MepConnectorEntity>();
    public DbSet<RoomEntity> Rooms => Set<RoomEntity>();
    public DbSet<ClashEntity> Clashes => Set<ClashEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrganizationEntity>(e =>
        {
            e.ToTable("organizations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
        });

        modelBuilder.Entity<UserEntity>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<ProjectEntity>(e =>
        {
            e.ToTable("projects");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired();
            e.HasOne(x => x.Organization).WithMany(o => o.Projects).HasForeignKey(x => x.OrganizationId);
        });

        modelBuilder.Entity<LevelEntity>(e =>
        {
            e.ToTable("levels");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Project).WithMany(p => p.Levels).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FamilyEntity>(e =>
        {
            e.ToTable("families");
            e.HasKey(x => x.Id);
        });

        modelBuilder.Entity<FamilyTypeEntity>(e =>
        {
            e.ToTable("family_types");
            e.HasKey(x => x.Id);
            e.Property(x => x.ParametersJson).HasColumnName("parameters").HasColumnType("jsonb");
            e.HasOne(x => x.Family).WithMany(f => f.Types).HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BimElementEntity>(e =>
        {
            e.ToTable("bim_elements");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IfcGuid).IsUnique();
            e.Property(x => x.ParametersJson).HasColumnName("parameters").HasColumnType("jsonb");
            e.Property(x => x.Placement).HasColumnType("geometry(PointZ,0)");
            e.Property(x => x.Bbox).HasColumnType("geometry(PolygonZ,0)");
            e.HasOne(x => x.Project).WithMany(p => p.Elements).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Level).WithMany().HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.FamilyType).WithMany().HasForeignKey(x => x.FamilyTypeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<UserEntity>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => x.DeletedAt == null); // docs §4.2 : bim_elements "WHERE deleted_at IS NULL"
        });

        modelBuilder.Entity<MepNetworkEntity>(e =>
        {
            e.ToTable("mep_networks");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Project).WithMany(p => p.Networks).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MepConnectorEntity>(e =>
        {
            e.ToTable("mep_connectors");
            e.HasKey(x => x.Id);
            e.Property(x => x.Position).HasColumnType("geometry(PointZ,0)").IsRequired();
            e.HasOne(x => x.Element).WithMany(el => el.Connectors).HasForeignKey(x => x.ElementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ConnectedTo).WithMany().HasForeignKey(x => x.ConnectedToId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Network).WithMany(n => n.Connectors).HasForeignKey(x => x.SystemId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RoomEntity>(e =>
        {
            e.ToTable("rooms");
            e.HasKey(x => x.Id);
            e.Property(x => x.Boundary).HasColumnType("geometry(PolygonZ,0)").IsRequired();
            e.HasOne(x => x.Project).WithMany(p => p.Rooms).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Level).WithMany().HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClashEntity>(e =>
        {
            e.ToTable("clashes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Location).HasColumnType("geometry(PointZ,0)");
            e.HasOne(x => x.Project).WithMany(p => p.Clashes).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ElementA).WithMany().HasForeignKey(x => x.ElementAId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ElementB).WithMany().HasForeignKey(x => x.ElementBId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
