using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

/// <summary>
/// Extrait la géométrie 3D triangulée d'un fichier IFC. Implémentation de
/// référence côté Infrastructure : IfcOpenShell (moteur open source
/// utilisé notamment par l'atelier BIM de FreeCAD), invoqué en sous-processus
/// Python — aucune bibliothèque .NET équivalente n'existe pour la géométrie IFC.
/// </summary>
public interface IIfcGeometryExtractor
{
    Task<IReadOnlyList<BuildingElement>> ExtractAsync(string ifcFilePath, CancellationToken ct = default);
}
