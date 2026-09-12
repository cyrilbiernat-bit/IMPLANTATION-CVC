using Bim.Cvc.Application;
using Bim.Cvc.Domain;
using Bim.Cvc.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Bim.Cvc.Infrastructure.Tests.Persistence;

/// <summary>
/// Tests d'intégration contre une vraie base PostgreSQL locale (bimcvc_test)
/// — pas de mock ni de provider InMemory : on veut prouver que les colonnes
/// jsonb (Calibration, VectorEntities), le tableau natif uuid[]
/// (ConnectedObjectIds) et les objets-valeur owned (Point2D) survivent
/// réellement un aller-retour en base. Chaque relecture se fait avec un
/// DbContext neuf, pour ne pas valider seulement le cache du change
/// tracker mais la persistance réelle.
/// </summary>
public sealed class EfCoreRepositoriesTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=bimcvc_test;Username=bimcvc;Password=bimcvc_dev_pw";

    public async Task InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static BimCvcDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BimCvcDbContext>().UseNpgsql(ConnectionString).Options;
        return new BimCvcDbContext(options);
    }

    [Fact]
    public async Task Project_round_trips_through_a_fresh_context()
    {
        var project = new Project { Name = "Tour Ariane — lot CVC" };
        await using (var db = CreateContext())
        {
            db.Projects.Add(project);
            await db.SaveChangesAsync();
        }

        await using var reload = CreateContext();
        var reloaded = await reload.Projects.FindAsync(project.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("Tour Ariane — lot CVC", reloaded!.Name);
    }

    [Fact]
    public async Task Drawing_persists_calibration_and_polymorphic_vector_entities_as_json()
    {
        var project = new Project { Name = "Projet" };
        var drawing = new Drawing
        {
            ProjectId = project.Id,
            FileName = "reseau.dxf",
            StoragePath = "memory://x",
            NbPages = 1,
            Format = PlanFormat.Dxf,
            VectorEntities = new List<PlanEntity>
            {
                new PlanLine(new Point2D(0, 0), new Point2D(10, 0)),
                new PlanCircle(new Point2D(5, 5), 2.5),
            },
        };
        drawing.Calibration = Calibration.Create(1, new CalibrationPoint(0, 0), new CalibrationPoint(100, 0), 10);

        await using (var db = CreateContext())
        {
            db.Projects.Add(project);
            db.Drawings.Add(drawing);
            await db.SaveChangesAsync();
        }

        await using var reload = CreateContext();
        var reloaded = await reload.Drawings.FindAsync(drawing.Id);

        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.Calibration);
        Assert.Equal(0.1, reloaded.Calibration!.MetersPerPixel, precision: 6);
        Assert.Equal(2, reloaded.VectorEntities!.Count);
        Assert.IsType<PlanLine>(reloaded.VectorEntities[0]);
        var circle = Assert.IsType<PlanCircle>(reloaded.VectorEntities[1]);
        Assert.Equal(2.5, circle.Radius);
    }

    [Fact]
    public async Task Layer_and_cvc_object_round_trip_with_owned_points_and_connections()
    {
        var project = new Project { Name = "Projet" };
        var drawing = new Drawing { ProjectId = project.Id, FileName = "plan.pdf", StoragePath = "memory://x", NbPages = 1 };
        var layer = new Layer { DrawingId = drawing.Id, Name = "Calque 1", Order = 0 };
        var accessory = new CvcObject
        {
            DrawingId = drawing.Id,
            LayerId = layer.Id,
            Type = CvcObjectType.Diffuseur,
            Position = new Point2D(12.5, 34.5),
            RotationRad = 1.2,
            DebitM3h = 500,
        };
        var duct = new CvcObject
        {
            DrawingId = drawing.Id,
            LayerId = layer.Id,
            Type = CvcObjectType.GaineCirculaire,
            Start = new Point2D(0, 0),
            End = new Point2D(50, 0),
            DiameterMm = 315,
        };
        duct.ConnectedObjectIds.Add(accessory.Id);
        accessory.ConnectedObjectIds.Add(duct.Id);

        await using (var db = CreateContext())
        {
            db.Projects.Add(project);
            db.Drawings.Add(drawing);
            db.Layers.Add(layer);
            db.CvcObjects.AddRange(accessory, duct);
            await db.SaveChangesAsync();
        }

        await using var reload = CreateContext();
        var reloadedDuct = await reload.CvcObjects.FindAsync(duct.Id);
        var reloadedAccessory = await reload.CvcObjects.FindAsync(accessory.Id);

        Assert.NotNull(reloadedDuct);
        Assert.Equal(0, reloadedDuct!.Start!.X);
        Assert.Equal(50, reloadedDuct.End!.X);
        Assert.Equal(315, reloadedDuct.DiameterMm);
        Assert.Contains(accessory.Id, reloadedDuct.ConnectedObjectIds);

        Assert.NotNull(reloadedAccessory);
        Assert.Equal(12.5, reloadedAccessory!.Position!.X);
        Assert.Equal(500, reloadedAccessory.DebitM3h);
        Assert.Contains(duct.Id, reloadedAccessory.ConnectedObjectIds);
    }

    [Fact]
    public async Task Deleting_a_project_cascades_to_its_drawings()
    {
        var project = new Project { Name = "Projet à supprimer" };
        var drawing = new Drawing { ProjectId = project.Id, FileName = "plan.pdf", StoragePath = "memory://x", NbPages = 1 };

        await using (var db = CreateContext())
        {
            db.Projects.Add(project);
            db.Drawings.Add(drawing);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            db.Projects.Remove((await db.Projects.FindAsync(project.Id))!);
            await db.SaveChangesAsync();
        }

        await using var reload = CreateContext();
        Assert.Null(await reload.Drawings.FindAsync(drawing.Id));
    }

    [Fact]
    public async Task Repositories_implement_the_application_interfaces_against_a_real_database()
    {
        await using var db = CreateContext();
        IProjectRepository projectRepo = new EfProjectRepository(db);
        IDrawingRepository drawingRepo = new EfDrawingRepository(db);
        ILayerRepository layerRepo = new EfLayerRepository(db);
        ICvcObjectRepository objectRepo = new EfCvcObjectRepository(db);

        var project = new Project { Name = "Via repositories" };
        projectRepo.Add(project);
        var drawing = new Drawing { ProjectId = project.Id, FileName = "plan.pdf", StoragePath = "memory://x", NbPages = 1 };
        drawingRepo.Add(drawing);
        var layer = new Layer { DrawingId = drawing.Id, Name = "Calque 1", Order = 0 };
        layerRepo.Add(layer);
        var obj = new CvcObject { DrawingId = drawing.Id, LayerId = layer.Id, Type = CvcObjectType.Bouche, Position = new Point2D(1, 1) };
        objectRepo.Add(obj);
        await db.SaveChangesAsync();

        Assert.Equal(project.Id, projectRepo.Get(project.Id)!.Id);
        Assert.Single(drawingRepo.GetByProject(project.Id));
        Assert.Single(layerRepo.GetByDrawing(drawing.Id));
        Assert.Single(objectRepo.GetByDrawing(drawing.Id));

        Assert.True(objectRepo.Remove(obj.Id));
        await db.SaveChangesAsync();
        Assert.Empty(objectRepo.GetByDrawing(drawing.Id));
    }
}
