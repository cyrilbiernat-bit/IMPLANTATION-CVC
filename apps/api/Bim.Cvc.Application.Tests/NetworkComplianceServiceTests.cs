using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Application.Tests;

public sealed class NetworkComplianceServiceTests
{
    private sealed record Sut(
        NetworkComplianceService Compliance,
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
        var compliance = new NetworkComplianceService(projects, drawings, objectRepository);
        return new Sut(compliance, projects, drawings, cvcObjects, layers);
    }

    private static Drawing AddDrawing(Sut sut, Guid projectId, string fileName = "reseau.pdf")
    {
        var drawing = new Drawing { ProjectId = projectId, FileName = fileName, StoragePath = "memory://x", NbPages = 1 };
        sut.Drawings.Add(drawing);
        return drawing;
    }

    [Fact]
    public void Check_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        Assert.Throws<ProjectNotFoundException>(() => sut.Compliance.Check(Guid.NewGuid()));
    }

    [Fact]
    public void Check_returns_no_finding_for_a_project_without_ducts()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Vide");
        AddDrawing(sut, project.Id);

        Assert.Empty(sut.Compliance.Check(project.Id));
    }

    [Fact]
    public void Check_flags_no_issue_when_the_explicit_velocity_is_within_range()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddDrawing(sut, project.Id);
        var layer = sut.Layers.CreateDefault(drawing.Id);
        sut.CvcObjects.AddDuct(
            drawing.Id, layer.Id, CvcObjectType.GaineCirculaire,
            new Point2D(0, 0), new Point2D(50, 0), null, null, 250, vitesseMs: 4.5);

        Assert.Empty(sut.Compliance.Check(project.Id));
    }

    [Fact]
    public void Check_warns_on_elevated_explicit_velocity()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddDrawing(sut, project.Id, "reseau-soufflage.pdf");
        var layer = sut.Layers.CreateDefault(drawing.Id);
        var duct = sut.CvcObjects.AddDuct(
            drawing.Id, layer.Id, CvcObjectType.GaineCirculaire,
            new Point2D(0, 0), new Point2D(50, 0), null, null, 250, vitesseMs: 7.5);

        var finding = Assert.Single(sut.Compliance.Check(project.Id));
        Assert.Equal(duct.Id, finding.ObjectId);
        Assert.Equal("reseau-soufflage.pdf", finding.DrawingFileName);
        Assert.Equal(ComplianceSeverity.Warning, finding.Severity);
        Assert.Equal("VitesseElevee", finding.RuleCode);
        Assert.Equal(7.5, finding.Value);
    }

    [Fact]
    public void Check_reports_critical_on_excessive_explicit_velocity()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddDrawing(sut, project.Id);
        var layer = sut.Layers.CreateDefault(drawing.Id);
        sut.CvcObjects.AddDuct(
            drawing.Id, layer.Id, CvcObjectType.GaineCirculaire,
            new Point2D(0, 0), new Point2D(50, 0), null, null, 250, vitesseMs: 12);

        var finding = Assert.Single(sut.Compliance.Check(project.Id));
        Assert.Equal(ComplianceSeverity.Critical, finding.Severity);
        Assert.Equal("VitesseExcessive", finding.RuleCode);
    }

    [Fact]
    public void Check_computes_velocity_from_debit_and_section_when_not_explicitly_set()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddDrawing(sut, project.Id);
        var layer = sut.Layers.CreateDefault(drawing.Id);
        // Section 0,5 m x 0,3 m = 0,15 m². Débit 5400 m³/h -> 1,5 m³/s / 0,15 m² = 10 m/s (critique).
        sut.CvcObjects.AddDuct(
            drawing.Id, layer.Id, CvcObjectType.GaineRectangulaire,
            new Point2D(0, 0), new Point2D(50, 0), widthMm: 500, heightMm: 300, diameterMm: null,
            debitM3h: 5400);

        var findings = sut.Compliance.Check(project.Id);
        var velocityFinding = Assert.Single(findings, f => f.RuleCode is "VitesseElevee" or "VitesseExcessive");
        Assert.Equal(10.0, velocityFinding.Value, precision: 6);
    }

    [Fact]
    public void Explicit_velocity_takes_precedence_over_the_computed_one()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddDrawing(sut, project.Id);
        var layer = sut.Layers.CreateDefault(drawing.Id);
        // Section/débit donneraient 10 m/s (critique) si on les utilisait, mais une vitesse explicite
        // de 2 m/s (dans les clous) est fournie et doit prévaloir.
        sut.CvcObjects.AddDuct(
            drawing.Id, layer.Id, CvcObjectType.GaineRectangulaire,
            new Point2D(0, 0), new Point2D(50, 0), widthMm: 500, heightMm: 300, diameterMm: null,
            debitM3h: 5400, vitesseMs: 2);

        Assert.Empty(sut.Compliance.Check(project.Id));
    }

    [Fact]
    public void Check_does_not_flag_velocity_when_neither_velocity_nor_debit_is_known()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddDrawing(sut, project.Id);
        var layer = sut.Layers.CreateDefault(drawing.Id);
        sut.CvcObjects.AddDuct(
            drawing.Id, layer.Id, CvcObjectType.GaineRectangulaire,
            new Point2D(0, 0), new Point2D(50, 0), widthMm: 500, heightMm: 300, diameterMm: null);

        Assert.Empty(sut.Compliance.Check(project.Id));
    }

    [Fact]
    public void Check_warns_on_excessive_aspect_ratio_for_a_rectangular_duct()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddDrawing(sut, project.Id);
        var layer = sut.Layers.CreateDefault(drawing.Id);
        var duct = sut.CvcObjects.AddDuct(
            drawing.Id, layer.Id, CvcObjectType.GaineRectangulaire,
            new Point2D(0, 0), new Point2D(50, 0), widthMm: 1000, heightMm: 150, diameterMm: null);

        var finding = Assert.Single(sut.Compliance.Check(project.Id));
        Assert.Equal(duct.Id, finding.ObjectId);
        Assert.Equal("RapportAspectExcessif", finding.RuleCode);
        Assert.Equal(ComplianceSeverity.Warning, finding.Severity);
        Assert.Equal(1000.0 / 150, finding.Value, precision: 6);
    }

    [Fact]
    public void Check_does_not_flag_a_reasonable_aspect_ratio()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddDrawing(sut, project.Id);
        var layer = sut.Layers.CreateDefault(drawing.Id);
        sut.CvcObjects.AddDuct(
            drawing.Id, layer.Id, CvcObjectType.GaineRectangulaire,
            new Point2D(0, 0), new Point2D(50, 0), widthMm: 400, heightMm: 250, diameterMm: null);

        Assert.Empty(sut.Compliance.Check(project.Id));
    }

    [Fact]
    public void Check_never_flags_aspect_ratio_for_a_circular_duct()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddDrawing(sut, project.Id);
        var layer = sut.Layers.CreateDefault(drawing.Id);
        sut.CvcObjects.AddDuct(
            drawing.Id, layer.Id, CvcObjectType.GaineCirculaire,
            new Point2D(0, 0), new Point2D(50, 0), null, null, 250, vitesseMs: 2);

        Assert.Empty(sut.Compliance.Check(project.Id));
    }

    [Fact]
    public void Check_ignores_non_duct_objects_even_with_a_high_velocity()
    {
        var sut = CreateSut();
        var project = sut.Projects.Create("Tour");
        var drawing = AddDrawing(sut, project.Id);
        var layer = sut.Layers.CreateDefault(drawing.Id);
        sut.CvcObjects.AddPointObject(drawing.Id, layer.Id, CvcObjectType.Bouche, new Point2D(0, 0), 0, vitesseMs: 15);

        Assert.Empty(sut.Compliance.Check(project.Id));
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
