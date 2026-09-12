using Bim.Cvc.Domain;
using Microsoft.EntityFrameworkCore;

namespace Bim.Cvc.Infrastructure.Persistence;

public sealed class BimCvcDbContext(DbContextOptions<BimCvcDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Drawing> Drawings => Set<Drawing>();
    public DbSet<Layer> Layers => Set<Layer>();
    public DbSet<CvcObject> CvcObjects => Set<CvcObject>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BimCvcDbContext).Assembly);
    }
}
