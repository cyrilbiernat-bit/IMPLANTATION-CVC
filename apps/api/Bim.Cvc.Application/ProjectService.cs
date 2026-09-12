using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public sealed class InvalidProjectException(string message) : Exception(message);

/// <summary>Un projet (module 4) regroupe les plans importés (module 1) sous un même nom de chantier.</summary>
public sealed class ProjectService(IProjectRepository repository)
{
    public Project Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidProjectException("Le nom du projet est requis.");
        }

        var project = new Project { Name = name.Trim() };
        repository.Add(project);
        return project;
    }

    public Project Get(Guid id) => repository.Get(id) ?? throw new ProjectNotFoundException(id);

    public IReadOnlyList<Project> GetAll() => repository.GetAll();
}
