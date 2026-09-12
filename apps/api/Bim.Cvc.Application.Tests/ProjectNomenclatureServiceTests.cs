using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Application.Tests;

public sealed class ProjectNomenclatureServiceTests
{
    private sealed record Sut(
        ProjectNomenclatureService Nomenclature,
        ProjectService Projects,
        FakeDrawingRepository Drawings,
        CvcObjectService CvcObjects,
        LayerService Layers);

    private static Sut CreateSut()
    {
        var projects = new ProjectService(new FakeProjectRepository());
        var drawings = new FakeDrawingRepository();
        var layerRepository = new FakeLayerRepository();
        var objectRepository = new FakeCvcObjectRepository();
        var layers = new LayerService(drawings, layerRepository, objectRepository);
        var cvcObjects = new CvcObjectService(drawings, objectRepository, layers);
        var nomenclature = new ProjectNomenclatureService(projects, drawings, objectRepository, cvcObjects, layers);
        return new Sut(nomenclature, projects, drawings, cvcObjects, layers);
    }

    private static Drawing AddCalibratedDrawing(Sut sut, Guid projectId, string fileName = "plan.pdf")
    {
        var drawing = new Drawing { ProjectId = projectId, FileName = fileName, StoragePath = "memory://x", NbPages = 1 };
        drawing.Calibration = Calibration.Create(1, new CalibrationPoint(0, 0), new CalibrationPoint(100, 0), 10);
        sut.Drawings.Add(drawing);
        return drawing;
    }

    [Fact]
    public void Compute_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        Assert.Throws<ProjectNotFoundException>(() => sut.Nomenclature.Compute(Guid.NewGuid()));
    }

    [Fact]
    public void Compute_returns_an_empty_list_for_a_project_without_objects()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Vide");
        AddCalibratedDrawing(sut, project.Id);

        Assert.Empty(sut.Nomenclature.Compute(project.Id));
    }

    [Fact]
    public void Compute_lists_one_row_per_object_with_drawing_and_layer_names()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddCalibratedDrawing(sut, project.Id, "reseau-cvc.pdf");
        var layer = sut.Layers.Create(drawing.Id, "Réseau soufflage");

        var duct = sut.CvcObjects.AddDuct(
            drawing.Id, layer.Id, CvcObjectType.GaineCirculaire,
            new Point2D(0, 0), new Point2D(50, 0), null, null, 315,
            debitM3h: 500, vitesseMs: 4.5, pressionPa: 120);

        var rows = sut.Nomenclature.Compute(project.Id);

        var row = Assert.Single(rows);
        Assert.Equal(duct.Id, row.ObjectId);
        Assert.Equal("reseau-cvc.pdf", row.DrawingFileName);
        Assert.Equal("Réseau soufflage", row.LayerName);
        Assert.Equal(CvcObjectType.GaineCirculaire, row.Type);
        Assert.Equal(315, row.DiameterMm);
        Assert.Equal(5, row.LengthMeters!.Value, precision: 6); // 50 px * 0,1 m/px
        Assert.Equal(500, row.DebitM3h);
        Assert.Equal(4.5, row.VitesseMs);
        Assert.Equal(120, row.PressionPa);
        Assert.True(row.WeightKg > 0);
        Assert.True(row.InsulationAreaM2 > 0);
    }

    [Fact]
    public void Compute_includes_rows_from_every_drawing_of_the_project()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawingA = AddCalibratedDrawing(sut, project.Id, "niveau-1.pdf");
        var drawingB = AddCalibratedDrawing(sut, project.Id, "niveau-2.pdf");
        var layerA = sut.Layers.CreateDefault(drawingA.Id);
        var layerB = sut.Layers.CreateDefault(drawingB.Id);

        sut.CvcObjects.AddPointObject(drawingA.Id, layerA.Id, CvcObjectType.Diffuseur, new Point2D(0, 0), 0);
        sut.CvcObjects.AddPointObject(drawingB.Id, layerB.Id, CvcObjectType.Bouche, new Point2D(0, 0), 0);

        var rows = sut.Nomenclature.Compute(project.Id);

        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.DrawingFileName == "niveau-1.pdf" && r.Type == CvcObjectType.Diffuseur);
        Assert.Contains(rows, r => r.DrawingFileName == "niveau-2.pdf" && r.Type == CvcObjectType.Bouche);
    }

    [Fact]
    public void Compute_only_includes_objects_belonging_to_the_requested_project()
    {
        var sut = CreateSut();
        var projectA = sut.Projects.Create("A");
        var projectB = sut.Projects.Create("B");
        var drawingA = AddCalibratedDrawing(sut, projectA.Id);
        var drawingB = AddCalibratedDrawing(sut, projectB.Id);
        var layerA = sut.Layers.CreateDefault(drawingA.Id);
        var layerB = sut.Layers.CreateDefault(drawingB.Id);
        sut.CvcObjects.AddPointObject(drawingA.Id, layerA.Id, CvcObjectType.Diffuseur, new Point2D(0, 0), 0);
        sut.CvcObjects.AddPointObject(drawingB.Id, layerB.Id, CvcObjectType.Bouche, new Point2D(0, 0), 0);

        var rowsA = sut.Nomenclature.Compute(projectA.Id);

        var row = Assert.Single(rowsA);
        Assert.Equal(CvcObjectType.Diffuseur, row.Type);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects = [];

        public void Add(Project project) => _projects[project.Id] = project;

        public Project? Get(Guid id) => _projects.GetValueOrDefault(id);

        public IReadOnlyList<Project> GetAll() => _projects.Values.ToList();
    }

    private sealed class FakeDrawingRepository : IDrawingRepository
    {
        private readonly Dictionary<Guid, Drawing> _drawings = [];

        public void Add(Drawing drawing) => _drawings[drawing.Id] = drawing;

        public Drawing? Get(Guid id) => _drawings.GetValueOrDefault(id);

        public IReadOnlyList<Drawing> GetByProject(Guid projectId) =>
            _drawings.Values.Where(d => d.ProjectId == projectId).ToList();
    }

    private sealed class FakeCvcObjectRepository : ICvcObjectRepository
    {
        private readonly Dictionary<Guid, CvcObject> _objects = [];

        public void Add(CvcObject cvcObject) => _objects[cvcObject.Id] = cvcObject;

        public CvcObject? Get(Guid id) => _objects.GetValueOrDefault(id);

        public IReadOnlyList<CvcObject> GetByDrawing(Guid drawingId) =>
            _objects.Values.Where(o => o.DrawingId == drawingId).ToList();

        public bool Remove(Guid id) => _objects.Remove(id);
    }

    private sealed class FakeLayerRepository : ILayerRepository
    {
        private readonly Dictionary<Guid, Layer> _layers = [];

        public void Add(Layer layer) => _layers[layer.Id] = layer;

        public Layer? Get(Guid id) => _layers.GetValueOrDefault(id);

        public IReadOnlyList<Layer> GetByDrawing(Guid drawingId) =>
            _layers.Values.Where(l => l.DrawingId == drawingId).OrderBy(l => l.Order).ToList();

        public bool Remove(Guid id) => _layers.Remove(id);
    }
}
