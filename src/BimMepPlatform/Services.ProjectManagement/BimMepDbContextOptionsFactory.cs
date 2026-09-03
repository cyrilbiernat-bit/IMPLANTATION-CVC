using Microsoft.EntityFrameworkCore;

namespace BimMep.Services.ProjectManagement;

/// <summary>
/// Construit les options du DbContext de maniere identique partout (outillage de migration, demo,
/// tests d'integration) : convention de nommage snake_case (docs 04-base-de-donnees.md — schema ecrit
/// en snake_case) et support PostGIS/NetTopologySuite pour les colonnes geometriques.
/// </summary>
public static class BimMepDbContextOptionsFactory
{
    public const string LocalDevConnectionString = "Host=localhost;Database=bimmep_dev;Username=bimmep;Password=bimmep_dev";

    public static DbContextOptions<BimMepDbContext> Build(string connectionString) =>
        new DbContextOptionsBuilder<BimMepDbContext>()
            .UseNpgsql(connectionString, o => o.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention()
            .Options;
}
