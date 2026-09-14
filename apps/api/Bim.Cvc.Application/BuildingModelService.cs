using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public sealed class InvalidBuildingModelException(string message) : Exception(message);

/// <summary>
/// Lot 2 — import d'un modèle de bâtiment IFC (CAO 3D, ex. FreeCAD/BIM,
/// Revit, ArchiCAD) : contexte architectural affiché dans la vue 3D aux
/// côtés du réseau CVC dessiné en 2D. La géométrie est extraite une fois à
/// l'import (via <see cref="IIfcGeometryExtractor"/>) et stockée déjà
/// triangulée — aucun traitement IFC n'est nécessaire à la lecture.
/// </summary>
public sealed class BuildingModelService(
    ProjectService projects,
    IBuildingModelRepository repository,
    IIfcGeometryExtractor extractor)
{
    private const long MaxSizeBytes = 200 * 1024 * 1024; // 200 Mo — un modèle de bâtiment est plus volumineux qu'un plan 2D.

    public async Task<BuildingModel> ImportAsync(Guid projectId, string fileName, Stream content, CancellationToken ct = default)
    {
        _ = projects.Get(projectId); // lève ProjectNotFoundException si le projet n'existe pas.

        if (!fileName.EndsWith(".ifc", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidBuildingModelException("Seul le format IFC (.ifc) est pris en charge pour le modèle de bâtiment.");
        }

        byte[] bytes;
        await using (var buffer = new MemoryStream())
        {
            await content.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
        }

        if ((long)bytes.Length is 0 or > MaxSizeBytes)
        {
            throw new InvalidBuildingModelException("Le fichier est vide ou dépasse la taille maximale (200 Mo).");
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.ifc");
        IReadOnlyList<BuildingElement> elements;
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, ct);
            try
            {
                elements = await extractor.ExtractAsync(tempPath, ct);
            }
            catch (Exception ex) when (ex is not InvalidBuildingModelException)
            {
                throw new InvalidBuildingModelException($"Impossible de lire ce fichier IFC : {ex.Message}");
            }
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }

        if (elements.Count == 0)
        {
            throw new InvalidBuildingModelException("Aucune géométrie exploitable n'a été trouvée dans ce fichier IFC.");
        }

        var existing = repository.GetByProject(projectId);
        if (existing is not null)
        {
            repository.Remove(existing.Id);
        }

        var model = new BuildingModel { ProjectId = projectId, FileName = fileName, Elements = elements };
        repository.Add(model);
        return model;
    }

    public BuildingModel? GetByProject(Guid projectId)
    {
        _ = projects.Get(projectId);
        return repository.GetByProject(projectId);
    }
}
