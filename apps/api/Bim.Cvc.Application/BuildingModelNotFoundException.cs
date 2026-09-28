namespace Bim.Cvc.Application;

public sealed class BuildingModelNotFoundException(Guid projectId) : Exception($"Aucun modèle de bâtiment pour le projet {projectId}.");
