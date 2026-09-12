using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure.Persistence;

public sealed class EfProjectRepository(BimCvcDbContext db) : IProjectRepository
{
    public void Add(Project project) => db.Projects.Add(project);

    public Project? Get(Guid id) => db.Projects.Find(id);

    public IReadOnlyList<Project> GetAll() => [.. db.Projects.OrderByDescending(p => p.CreatedAt)];
}
