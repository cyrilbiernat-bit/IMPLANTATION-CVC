namespace Bim.Cvc.Application;

public sealed class ProjectNotFoundException(Guid projectId) : Exception($"Projet introuvable : {projectId}");
