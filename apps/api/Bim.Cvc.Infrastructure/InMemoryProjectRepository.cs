using System.Collections.Concurrent;
using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure;

/// <summary>
/// Stand-in de développement pour la persistance PostgreSQL. Les données ne
/// survivent pas au redémarrage du processus.
/// </summary>
public sealed class InMemoryProjectRepository : IProjectRepository
{
    private readonly ConcurrentDictionary<Guid, Project> _projects = new();

    public void Add(Project project) => _projects[project.Id] = project;

    public Project? Get(Guid id) => _projects.GetValueOrDefault(id);

    public IReadOnlyList<Project> GetAll() => _projects.Values.OrderByDescending(p => p.CreatedAt).ToList();
}
