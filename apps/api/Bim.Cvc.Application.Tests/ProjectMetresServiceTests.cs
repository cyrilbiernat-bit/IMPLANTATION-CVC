using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Application.Tests;

public sealed class ProjectMetresServiceTests
{
    private sealed record Sut(
        ProjectMetresService MetresService,
        ProjectService Projects,
        FakeDrawingRepository Drawings,
        CvcObjectService CvcObjects,
        LayerService Layers);

    private static Sut CreateSut()
    {
        var projectRepository = new FakeProjectRepository();
        var projects = new ProjectService(projectRepository);
        var drawings = new FakeDrawingRepository();
        var layerRepository = new FakeLayerRepository();
        var objectRepository = new FakeCvcObjectRepository();
        var layers = new LayerService(drawings, layerRepository, objectRepository);
        var cvcObjects = new CvcObjectService(drawings, objectRepository, layers);
        var metres = new ProjectMetresService(projects, drawings, objectRepository, cvcObjects);
        return new Sut(metres, projects, drawings, cvcObjects, layers);
    }

    private static Drawing AddCalibratedDrawing(Sut sut, Guid projectId)
    {
        var drawing = new Drawing { ProjectId = projectId, FileName = "plan.pdf", StoragePath = "memory://x", NbPages = 1 };
        // 100 px pour 10 m -> 0,1 m/px.
        drawing.Calibration = Calibration.Create(1, new CalibrationPoint(0, 0), new CalibrationPoint(100, 0), 10);
        sut.Drawings.Add(drawing);
        return drawing;
    }

    [Fact]
    public void Compute_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        Assert.Throws<ProjectNotFoundException>(() => sut.MetresService.Compute(Guid.NewGuid()));
    }

    [Fact]
    public void Compute_returns_zeros_for_a_project_without_drawings()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Vide");

        var summary = sut.MetresService.Compute(project.Id);

        Assert.Equal(0, summary.DrawingCount);
        Assert.Equal(0, summary.TotalDuctLengthMeters);
        Assert.Empty(summary.AccessoryCounts);
    }

    [Fact]
    public void Compute_sums_duct_length_area_and_weight_across_all_drawings_of_the_project()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawingA = AddCalibratedDrawing(sut, project.Id);
        var drawingB = AddCalibratedDrawing(sut, project.Id);
        var layerA = sut.Layers.CreateDefault(drawingA.Id);
        var layerB = sut.Layers.CreateDefault(drawingB.Id);

        // 50 px = 5 m sur chaque plan.
        sut.CvcObjects.AddDuct(drawingA.Id, layerA.Id, CvcObjectType.GaineCirculaire, new Point2D(0, 0), new Point2D(50, 0), null, null, 315);
        sut.CvcObjects.AddDuct(drawingB.Id, layerB.Id, CvcObjectType.GaineCirculaire, new Point2D(0, 0), new Point2D(50, 0), null, null, 315);

        var summary = sut.MetresService.Compute(project.Id);

        Assert.Equal(2, summary.DrawingCount);
        Assert.Equal(10, summary.TotalDuctLengthMeters, precision: 6); // 5 m + 5 m
        Assert.True(summary.TotalInsulationAreaM2 > 0);
        Assert.True(summary.TotalWeightKg > 0);
    }

    [Fact]
    public void Compute_counts_accessories_by_type()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddCalibratedDrawing(sut, project.Id);
        var layer = sut.Layers.CreateDefault(drawing.Id);

        sut.CvcObjects.AddPointObject(drawing.Id, layer.Id, CvcObjectType.Diffuseur, new Point2D(0, 0), 0);
        sut.CvcObjects.AddPointObject(drawing.Id, layer.Id, CvcObjectType.Diffuseur, new Point2D(10, 10), 0);
        sut.CvcObjects.AddPointObject(drawing.Id, layer.Id, CvcObjectType.Bouche, new Point2D(20, 20), 0);

        var summary = sut.MetresService.Compute(project.Id);

        Assert.Contains(summary.AccessoryCounts, a => a.Type == CvcObjectType.Diffuseur && a.Count == 2);
        Assert.Contains(summary.AccessoryCounts, a => a.Type == CvcObjectType.Bouche && a.Count == 1);
    }

    [Fact]
    public void Compute_ignores_length_of_ducts_on_uncalibrated_drawings()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = new Drawing { ProjectId = project.Id, FileName = "plan.pdf", StoragePath = "memory://x", NbPages = 1 };
        sut.Drawings.Add(drawing);
        var layer = sut.Layers.CreateDefault(drawing.Id);

        sut.CvcObjects.AddDuct(drawing.Id, layer.Id, CvcObjectType.GaineCirculaire, new Point2D(0, 0), new Point2D(50, 0), null, null, 315);

        var summary = sut.MetresService.Compute(project.Id);

        Assert.Equal(0, summary.TotalDuctLengthMeters);
        Assert.Equal(0, summary.TotalInsulationAreaM2);
    }

    [Fact]
    public void Compute_only_aggregates_drawings_belonging_to_the_requested_project()
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

        var summaryA = sut.MetresService.Compute(projectA.Id);

        Assert.Equal(1, summaryA.DrawingCount);
        Assert.Contains(summaryA.AccessoryCounts, a => a.Type == CvcObjectType.Diffuseur);
        Assert.DoesNotContain(summaryA.AccessoryCounts, a => a.Type == CvcObjectType.Bouche);
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
