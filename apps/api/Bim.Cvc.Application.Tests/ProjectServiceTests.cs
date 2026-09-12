using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Application.Tests;

public sealed class ProjectServiceTests
{
    private static ProjectService CreateSut() => new(new FakeProjectRepository());

    [Fact]
    public void Create_stores_a_project_with_the_given_name()
    {
        var sut = CreateSut();

        var project = sut.Create("Tour Ariane — lot CVC");

        Assert.Equal("Tour Ariane — lot CVC", project.Name);
        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Same(project, sut.Get(project.Id));
    }

    [Fact]
    public void Create_trims_the_name()
    {
        var sut = CreateSut();

        var project = sut.Create("  Chantier  ");

        Assert.Equal("Chantier", project.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_rejects_a_blank_name(string? name)
    {
        var sut = CreateSut();

        Assert.Throws<InvalidProjectException>(() => sut.Create(name!));
    }

    [Fact]
    public void Get_throws_for_an_unknown_project()
    {
        var sut = CreateSut();

        Assert.Throws<ProjectNotFoundException>(() => sut.Get(Guid.NewGuid()));
    }

    [Fact]
    public void GetAll_returns_every_created_project()
    {
        var sut = CreateSut();
        var a = sut.Create("Projet A");
        var b = sut.Create("Projet B");

        var all = sut.GetAll();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, p => p.Id == a.Id);
        Assert.Contains(all, p => p.Id == b.Id);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        private readonly Dictionary<Guid, Project> _projects = [];

        public void Add(Project project) => _projects[project.Id] = project;

        public Project? Get(Guid id) => _projects.GetValueOrDefault(id);

        public IReadOnlyList<Project> GetAll() => _projects.Values.ToList();
    }
}
