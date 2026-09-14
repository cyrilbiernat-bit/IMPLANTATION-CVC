using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Application.Tests;

public sealed class BuildingModelServiceTests
{
    private static readonly BuildingElement SomeWall = new(
        "IfcWall", "Mur 1", Positions: [0, 0, 0, 1, 0, 0, 1, 1, 0], Indices: [0, 1, 2]);

    private static (BuildingModelService sut, FakeBuildingModelRepository repository, FakeExtractor extractor, Guid projectId) CreateSut()
    {
        var projects = new ProjectService(new FakeProjectRepository());
        var project = projects.Create("Chantier test");
        var repository = new FakeBuildingModelRepository();
        var extractor = new FakeExtractor([SomeWall]);
        var sut = new BuildingModelService(projects, repository, extractor);
        return (sut, repository, extractor, project.Id);
    }

    private static MemoryStream SomeBytes(int length = 16) => new(new byte[length]);

    [Fact]
    public async Task ImportAsync_stores_the_extracted_elements()
    {
        var (sut, _, _, projectId) = CreateSut();

        var model = await sut.ImportAsync(projectId, "batiment.ifc", SomeBytes());

        Assert.Equal(projectId, model.ProjectId);
        Assert.Equal("batiment.ifc", model.FileName);
        var element = Assert.Single(model.Elements);
        Assert.Equal("IfcWall", element.IfcType);
    }

    [Fact]
    public async Task ImportAsync_throws_when_the_project_does_not_exist()
    {
        var (sut, _, _, _) = CreateSut();

        await Assert.ThrowsAsync<ProjectNotFoundException>(() => sut.ImportAsync(Guid.NewGuid(), "batiment.ifc", SomeBytes()));
    }

    [Theory]
    [InlineData("batiment.rvt")]
    [InlineData("batiment.dwg")]
    [InlineData("batiment")]
    public async Task ImportAsync_rejects_non_ifc_filenames(string fileName)
    {
        var (sut, _, _, projectId) = CreateSut();

        await Assert.ThrowsAsync<InvalidBuildingModelException>(() => sut.ImportAsync(projectId, fileName, SomeBytes()));
    }

    [Fact]
    public async Task ImportAsync_rejects_an_empty_file()
    {
        var (sut, _, _, projectId) = CreateSut();

        await Assert.ThrowsAsync<InvalidBuildingModelException>(() => sut.ImportAsync(projectId, "batiment.ifc", new MemoryStream()));
    }

    [Fact]
    public async Task ImportAsync_rejects_a_file_whose_extraction_yields_no_geometry()
    {
        var projects = new ProjectService(new FakeProjectRepository());
        var project = projects.Create("Chantier test");
        var repository = new FakeBuildingModelRepository();
        var extractor = new FakeExtractor([]);
        var sut = new BuildingModelService(projects, repository, extractor);

        await Assert.ThrowsAsync<InvalidBuildingModelException>(() => sut.ImportAsync(project.Id, "batiment.ifc", SomeBytes()));
    }

    [Fact]
    public async Task ImportAsync_wraps_extractor_failures_as_InvalidBuildingModelException()
    {
        var projects = new ProjectService(new FakeProjectRepository());
        var project = projects.Create("Chantier test");
        var repository = new FakeBuildingModelRepository();
        var extractor = new FakeExtractor(elements: null, failure: new InvalidOperationException("python3 introuvable"));
        var sut = new BuildingModelService(projects, repository, extractor);

        var ex = await Assert.ThrowsAsync<InvalidBuildingModelException>(() => sut.ImportAsync(project.Id, "batiment.ifc", SomeBytes()));
        Assert.Contains("python3 introuvable", ex.Message);
    }

    [Fact]
    public async Task ImportAsync_replaces_any_existing_model_for_the_same_project()
    {
        var (sut, repository, _, projectId) = CreateSut();
        var first = await sut.ImportAsync(projectId, "v1.ifc", SomeBytes());

        var second = await sut.ImportAsync(projectId, "v2.ifc", SomeBytes());

        Assert.Null(repository.Get(first.Id));
        Assert.Same(second, repository.GetByProject(projectId));
        Assert.Equal("v2.ifc", repository.GetByProject(projectId)!.FileName);
    }

    [Fact]
    public void GetByProject_returns_null_when_nothing_was_imported()
    {
        var (sut, _, _, projectId) = CreateSut();

        Assert.Null(sut.GetByProject(projectId));
    }

    [Fact]
    public void GetByProject_throws_when_the_project_does_not_exist()
    {
        var (sut, _, _, _) = CreateSut();

        Assert.Throws<ProjectNotFoundException>(() => sut.GetByProject(Guid.NewGuid()));
    }

    private sealed class FakeExtractor(IReadOnlyList<BuildingElement>? elements, Exception? failure = null) : IIfcGeometryExtractor
    {
        public Task<IReadOnlyList<BuildingElement>> ExtractAsync(string ifcFilePath, CancellationToken ct = default) =>
            failure is not null ? Task.FromException<IReadOnlyList<BuildingElement>>(failure) : Task.FromResult(elements!);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects = [];

        public void Add(Project project) => _projects[project.Id] = project;

        public Project? Get(Guid id) => _projects.GetValueOrDefault(id);

        public IReadOnlyList<Project> GetAll() => _projects.Values.ToList();
    }

    private sealed class FakeBuildingModelRepository : IBuildingModelRepository
    {
        private readonly Dictionary<Guid, BuildingModel> _models = [];

        public void Add(BuildingModel model) => _models[model.Id] = model;

        public BuildingModel? Get(Guid id) => _models.GetValueOrDefault(id);

        public BuildingModel? GetByProject(Guid projectId) => _models.Values.FirstOrDefault(m => m.ProjectId == projectId);

        public bool Remove(Guid id) => _models.Remove(id);
    }
}
