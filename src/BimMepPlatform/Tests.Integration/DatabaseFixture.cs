using BimMep.Services.ProjectManagement;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BimMep.Tests.Integration;

/// <summary>
/// Ces tests necessitent un PostgreSQL (+ extension PostGIS) reellement accessible — ce ne sont pas
/// des tests unitaires purs comme ceux du projet Tests/ (docs §13-modules-critiques.md). Par defaut,
/// ils visent une instance locale de developpement (voir <see cref="BimMepDbContextOptionsFactory.LocalDevConnectionString"/>) ;
/// surchargeable via la variable d'environnement BIMMEP_CONNECTION_STRING. Sans base disponible, ces
/// tests echouent avec une erreur de connexion explicite — c'est le comportement attendu (docs §22
/// "environnement de developpement requis"), pas un flake a masquer.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public string ConnectionString { get; } =
        Environment.GetEnvironmentVariable("BIMMEP_CONNECTION_STRING") ?? BimMepDbContextOptionsFactory.LocalDevConnectionString;

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public BimMepDbContext CreateContext() => new(BimMepDbContextOptionsFactory.Build(ConnectionString));
}

[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>
{
}
