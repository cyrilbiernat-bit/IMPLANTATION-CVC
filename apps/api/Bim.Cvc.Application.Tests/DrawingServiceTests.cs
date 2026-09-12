using System.Text;
using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Application.Tests;

public sealed class DrawingServiceTests
{
    private static (DrawingService sut, Guid projectId) CreateSut(int pageCount = 1, IDrawingRepository? repository = null)
    {
        var projects = new ProjectService(new FakeProjectRepository());
        var project = projects.Create("Chantier test");
        var sut = new DrawingService(new FakeFileStore(), repository ?? new FakeRepository(), [new FakePlanParser(pageCount)], projects);
        return (sut, project.Id);
    }

    private static MemoryStream SomeBytes(int length = 16) => new(new byte[length]);

    [Fact]
    public async Task ImportAsync_stores_drawing_and_returns_page_count()
    {
        var (sut, projectId) = CreateSut(pageCount: 7);

        var drawing = await sut.ImportAsync(projectId, "reseau-cvc.pdf", SomeBytes());

        Assert.Equal("reseau-cvc.pdf", drawing.FileName);
        Assert.Equal(projectId, drawing.ProjectId);
        Assert.Equal(7, drawing.NbPages);
        Assert.Equal(PlanFormat.Pdf, drawing.Format);
        Assert.NotEqual(Guid.Empty, drawing.Id);
    }

    [Fact]
    public async Task ImportAsync_persists_the_drawing_in_the_repository()
    {
        var repository = new FakeRepository();
        var (sut, projectId) = CreateSut(repository: repository);

        var drawing = await sut.ImportAsync(projectId, "plan.pdf", SomeBytes());

        Assert.Same(drawing, repository.Get(drawing.Id));
    }

    [Fact]
    public async Task ImportAsync_throws_when_the_project_does_not_exist()
    {
        var (sut, _) = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.ImportAsync(Guid.NewGuid(), "plan.pdf", SomeBytes()));
    }

    [Theory]
    [InlineData("plan.png")]
    [InlineData("plan")]
    [InlineData("plan.PDF.exe")]
    public async Task ImportAsync_rejects_unsupported_file_extensions(string fileName)
    {
        var (sut, projectId) = CreateSut();

        await Assert.ThrowsAsync<InvalidDrawingException>(() => sut.ImportAsync(projectId, fileName, SomeBytes()));
    }

    [Theory]
    [InlineData("plan.ifc")]
    [InlineData("maquette.rvt")]
    public async Task ImportAsync_rejects_ifc_and_revit_with_a_helpful_message(string fileName)
    {
        var (sut, projectId) = CreateSut();

        var ex = await Assert.ThrowsAsync<InvalidDrawingException>(() => sut.ImportAsync(projectId, fileName, SomeBytes()));
        Assert.Contains("export", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportAsync_rejects_an_empty_file()
    {
        var (sut, projectId) = CreateSut();

        await Assert.ThrowsAsync<InvalidDrawingException>(() => sut.ImportAsync(projectId, "plan.pdf", new MemoryStream()));
    }

    [Fact]
    public async Task ImportAsync_rejects_a_plan_with_no_pages()
    {
        var (sut, projectId) = CreateSut(pageCount: 0);

        await Assert.ThrowsAsync<InvalidDrawingException>(() => sut.ImportAsync(projectId, "plan.pdf", SomeBytes()));
    }

    [Fact]
    public async Task ImportAsync_routes_dxf_files_to_the_matching_parser()
    {
        var repository = new FakeRepository();
        var projects = new ProjectService(new FakeProjectRepository());
        var project = projects.Create("Chantier test");
        var sut = new DrawingService(
            new FakeFileStore(),
            repository,
            [new FakePlanParser(1, PlanFormat.Pdf), new FakePlanParser(1, PlanFormat.Dxf)],
            projects);

        var drawing = await sut.ImportAsync(project.Id, "reseau.dxf", SomeBytes());

        Assert.Equal(PlanFormat.Dxf, drawing.Format);
    }

    private sealed class FakeFileStore : IDrawingFileStore
    {
        public Task<string> SaveAsync(Guid drawingId, string fileName, Stream content, CancellationToken ct = default)
            => Task.FromResult($"memory://{drawingId}");

        public Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default)
            => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("fake-plan")));
    }

    private sealed class FakeRepository : IDrawingRepository
    {
        private readonly Dictionary<Guid, Drawing> _drawings = [];

        public void Add(Drawing drawing) => _drawings[drawing.Id] = drawing;

        public Drawing? Get(Guid id) => _drawings.GetValueOrDefault(id);

        public IReadOnlyList<Drawing> GetByProject(Guid projectId) =>
            _drawings.Values.Where(d => d.ProjectId == projectId).ToList();
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects = [];

        public void Add(Project project) => _projects[project.Id] = project;

        public Project? Get(Guid id) => _projects.GetValueOrDefault(id);

        public IReadOnlyList<Project> GetAll() => _projects.Values.ToList();
    }

    private sealed class FakePlanParser(int pageCount, PlanFormat format = PlanFormat.Pdf) : IPlanFileParser
    {
        public bool CanParse(PlanFormat candidate) => candidate == format;

        public ParsedPlan Parse(PlanFormat candidate, Stream content) => new(pageCount, Entities: null);
    }
}
