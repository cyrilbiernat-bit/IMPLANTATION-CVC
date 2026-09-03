using BimMep.Core.Bim;
using BimMep.Core.Geometry;
using BimMep.Core.Mep;
using BimMep.Services.ProjectManagement;
using BimMep.Services.ProjectManagement.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BimMep.Tests.Integration;

[Collection("Database")]
public class ProjectPersistenceTests
{
    private readonly DatabaseFixture _fixture;

    public ProjectPersistenceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task<(Guid organizationId, Guid userId)> SeedOrganizationAndUserAsync(BimMepDbContext db)
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity { Id = organizationId, Name = $"Org {organizationId}", CreatedAt = DateTimeOffset.UtcNow });
        db.Users.Add(new UserEntity { Id = userId, Email = $"{userId}@test.local", DisplayName = "Utilisateur de test" });
        await db.SaveChangesAsync();
        return (organizationId, userId);
    }

    [Fact]
    public async Task SaveAndReload_DuctDimensionsMaterialAndPlacement_SurviveRoundTrip()
    {
        await using var db = _fixture.CreateContext();
        var (organizationId, userId) = await SeedOrganizationAndUserAsync(db);

        var family = new Family { Name = "Gaine rectangulaire", Category = "duct" };
        var type = family.AddType("Generique");
        var duct = new MepDuct("Troncon test", type, DuctShape.Rectangular, lengthM: 6.0) { InsulationThicknessM = 0.02 };
        duct.ResizeRectangular(0.8, 0.4);
        duct.Placement = new Transform3D(new Point3D(1.5, 2.5, 3.0), 0);

        var project = new Project { Name = $"IT-{ProjectSuffix()}" };
        project.Elements.Add(duct);

        await new ProjectRepository(db).SaveNewProjectAsync(project, organizationId, userId);

        await using var reloadDb = _fixture.CreateContext();
        var reloaded = await new ProjectRepository(reloadDb).LoadProjectAsync(project.Id);

        Assert.NotNull(reloaded);
        var reloadedDuct = Assert.IsType<MepDuct>(Assert.Single(reloaded!.Elements));
        Assert.Equal(0.8, reloadedDuct.WidthM, precision: 6);
        Assert.Equal(0.4, reloadedDuct.HeightM, precision: 6);
        Assert.Equal(6.0, reloadedDuct.LengthM, precision: 6);
        Assert.Equal(0.02, reloadedDuct.InsulationThicknessM, precision: 6);
        Assert.Equal("Acier galvanise", reloadedDuct.Material);
        Assert.Equal(1.5, reloadedDuct.Placement.Origin.X, precision: 6);
        Assert.Equal(2.5, reloadedDuct.Placement.Origin.Y, precision: 6);
        Assert.Equal(3.0, reloadedDuct.Placement.Origin.Z, precision: 6);
    }

    [Fact]
    public async Task SaveAndReload_CircularDuct_RoundTripsDiameter()
    {
        await using var db = _fixture.CreateContext();
        var (organizationId, userId) = await SeedOrganizationAndUserAsync(db);

        var family = new Family { Name = "Gaine circulaire", Category = "duct" };
        var type = family.AddType("Generique");
        var duct = new MepDuct("D-circ", type, DuctShape.Circular, lengthM: 3.0);
        duct.ResizeCircular(0.315);

        var project = new Project { Name = $"IT-{ProjectSuffix()}" };
        project.Elements.Add(duct);

        await new ProjectRepository(db).SaveNewProjectAsync(project, organizationId, userId);

        await using var reloadDb = _fixture.CreateContext();
        var reloaded = await new ProjectRepository(reloadDb).LoadProjectAsync(project.Id);
        var reloadedDuct = Assert.IsType<MepDuct>(Assert.Single(reloaded!.Elements));

        Assert.Equal(DuctShape.Circular, reloadedDuct.Shape);
        Assert.Equal(0.315, reloadedDuct.DiameterM, precision: 6);
    }

    [Fact]
    public async Task SaveAndReload_Pipe_RoundTripsSystemTypeAndSlope()
    {
        await using var db = _fixture.CreateContext();
        var (organizationId, userId) = await SeedOrganizationAndUserAsync(db);

        var pipe = new MepPipe("Colonne", null, SystemClassification.WasteEv, lengthM: 9.0) { DiameterNominalM = 0.1 };

        var project = new Project { Name = $"IT-{ProjectSuffix()}" };
        project.Elements.Add(pipe);

        await new ProjectRepository(db).SaveNewProjectAsync(project, organizationId, userId);

        await using var reloadDb = _fixture.CreateContext();
        var reloaded = await new ProjectRepository(reloadDb).LoadProjectAsync(project.Id);
        var reloadedPipe = Assert.IsType<MepPipe>(Assert.Single(reloaded!.Elements));

        Assert.Equal(SystemClassification.WasteEv, reloadedPipe.SystemType);
        Assert.Equal(9.0, reloadedPipe.LengthM, precision: 6);
        Assert.Equal(1.0, reloadedPipe.SlopePercent, precision: 6); // pente minimale par defaut (docs MepPipe)
    }

    [Fact]
    public async Task SaveNewProject_MutuallyConnectedConnectors_ResolveCrossReferencesInSecondPass()
    {
        await using var db = _fixture.CreateContext();
        var (organizationId, userId) = await SeedOrganizationAndUserAsync(db);

        var family = new Family { Name = "Gaine rectangulaire", Category = "duct" };
        var type = family.AddType("Generique");

        var duct1 = new MepDuct("D1", type, DuctShape.Rectangular, lengthM: 4.0);
        duct1.ResizeRectangular(0.6, 0.3);
        var outlet = duct1.AddConnector(new Point3D(4, 0, 0), new Vector3D(1, 0, 0), SystemClassification.SupplyAir);

        var duct2 = new MepDuct("D2", type, DuctShape.Rectangular, lengthM: 4.0);
        duct2.ResizeRectangular(0.6, 0.3);
        var inlet = duct2.AddConnector(new Point3D(0, 0, 0), new Vector3D(-1, 0, 0), SystemClassification.SupplyAir);
        outlet.ConnectTo(inlet);

        var project = new Project { Name = $"IT-{ProjectSuffix()}" };
        project.Elements.Add(duct1);
        project.Elements.Add(duct2);

        // Ne doit pas lever InvalidOperationException (dependance circulaire) — c'est le bug reel
        // trouve lors de la validation contre PostgreSQL (docs 13-modules-critiques.md).
        await new ProjectRepository(db).SaveNewProjectAsync(project, organizationId, userId);

        await using var checkDb = _fixture.CreateContext();
        var persistedOutlet = await checkDb.MepConnectors.SingleAsync(c => c.Id == outlet.Id);
        var persistedInlet = await checkDb.MepConnectors.SingleAsync(c => c.Id == inlet.Id);

        Assert.Equal(inlet.Id, persistedOutlet.ConnectedToId);
        Assert.Equal(outlet.Id, persistedInlet.ConnectedToId);
    }

    [Fact]
    public async Task SaveAndReload_RoomBoundary_RoundTripsPolygon()
    {
        await using var db = _fixture.CreateContext();
        var (organizationId, userId) = await SeedOrganizationAndUserAsync(db);

        var level = new Level { Name = "RDC", ElevationMeters = 0, HeightMeters = 3 };
        var room = new Room
        {
            Name = "Bureau 101",
            Level = level,
            Boundary = new[]
            {
                new Point3D(0, 0, 0), new Point3D(5, 0, 0), new Point3D(5, 4, 0), new Point3D(0, 4, 0)
            },
            AreaM2 = 20.0,
        };

        var project = new Project { Name = $"IT-{ProjectSuffix()}" };
        project.Levels.Add(level);
        project.Rooms.Add(room);

        await new ProjectRepository(db).SaveNewProjectAsync(project, organizationId, userId);

        await using var reloadDb = _fixture.CreateContext();
        var reloaded = await new ProjectRepository(reloadDb).LoadProjectAsync(project.Id);

        var reloadedRoom = Assert.Single(reloaded!.Rooms);
        Assert.Equal("Bureau 101", reloadedRoom.Name);
        Assert.Equal(20.0, reloadedRoom.AreaM2, precision: 6);
        Assert.Equal(4, reloadedRoom.Boundary.Count);
        Assert.Contains(reloadedRoom.Boundary, p => Math.Abs(p.X - 5) < 1e-6 && Math.Abs(p.Y - 4) < 1e-6);
    }

    [Fact]
    public async Task Save_DuplicateIfcGuid_ViolatesUniqueConstraint()
    {
        // Opere directement au niveau entite (plutot que via le domaine, ou IfcGuid est en lecture
        // seule par conception — docs §5.1 "jamais regenere") pour verifier que la contrainte unique
        // "ix_bim_elements_ifc_guid" generee par la migration est reellement appliquee par PostgreSQL,
        // pas seulement documentee dans le SQL de reference (docs §6.4).
        await using var db = _fixture.CreateContext();
        var (organizationId, userId) = await SeedOrganizationAndUserAsync(db);

        var projectEntity = new ProjectEntity
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, Name = $"IT-{ProjectSuffix()}",
            Phase = "APS", LodTarget = 100, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Projects.Add(projectEntity);
        await db.SaveChangesAsync();

        string duplicateGuid = IfcGuidGenerator.NewGuid();
        var now = DateTimeOffset.UtcNow;
        BimElementEntity MakeElement() => new()
        {
            Id = Guid.NewGuid(), IfcGuid = duplicateGuid, ProjectId = projectEntity.Id, Category = "MepDuct",
            Lod = 100, ParametersJson = "{}", RevisionNumber = 1, CreatedBy = userId, CreatedAt = now, UpdatedAt = now,
        };

        db.BimElements.Add(MakeElement());
        await db.SaveChangesAsync();

        db.BimElements.Add(MakeElement());
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static string ProjectSuffix() => Guid.NewGuid().ToString("N")[..8];
}
