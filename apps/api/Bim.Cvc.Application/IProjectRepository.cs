using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public interface IProjectRepository
{
    void Add(Project project);

    Project? Get(Guid id);

    IReadOnlyList<Project> GetAll();
}
