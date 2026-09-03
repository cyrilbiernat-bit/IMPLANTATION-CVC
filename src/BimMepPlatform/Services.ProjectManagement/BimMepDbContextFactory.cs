using Microsoft.EntityFrameworkCore.Design;

namespace BimMep.Services.ProjectManagement;

/// <summary>
/// Fabrique de conception pour les outils EF Core (`dotnet ef migrations add`, `dotnet ef database
/// update`) : ils instancient le DbContext hors de tout conteneur d'injection de dependances. La
/// chaine de connexion se lit dans la variable d'environnement BIMMEP_CONNECTION_STRING (jamais en
/// dur dans le code, docs §15.8 securite) avec un repli developpement local explicite.
/// </summary>
public sealed class BimMepDbContextFactory : IDesignTimeDbContextFactory<BimMepDbContext>
{
    public BimMepDbContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("BIMMEP_CONNECTION_STRING")
            ?? BimMepDbContextOptionsFactory.LocalDevConnectionString;

        return new BimMepDbContext(BimMepDbContextOptionsFactory.Build(connectionString));
    }
}
