using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public sealed record AccessoryCount(CvcObjectType Type, int Count);

public sealed record ProjectMetresSummary(
    Guid ProjectId,
    int DrawingCount,
    double TotalDuctLengthMeters,
    double TotalInsulationAreaM2,
    double TotalWeightKg,
    IReadOnlyList<AccessoryCount> AccessoryCounts);

/// <summary>
/// Module 5 — métrés : quantités agrégées sur l'ensemble des plans d'un
/// projet (longueur de réseau, surface à calorifuger, poids, décompte des
/// accessoires/terminaux/équipements).
/// </summary>
public sealed class ProjectMetresService(
    ProjectService projects,
    IDrawingRepository drawings,
    ICvcObjectRepository objects,
    CvcObjectService cvcObjectService)
{
    public ProjectMetresSummary Compute(Guid projectId)
    {
        _ = projects.Get(projectId); // lève ProjectNotFoundException si le projet n'existe pas.

        var projectDrawings = drawings.GetByProject(projectId);
        double totalLength = 0;
        double totalArea = 0;
        double totalWeight = 0;
        var accessoryCounts = new Dictionary<CvcObjectType, int>();

        foreach (var drawing in projectDrawings)
        {
            foreach (var obj in objects.GetByDrawing(drawing.Id))
            {
                if (CvcObject.IsDuct(obj.Type))
                {
                    totalLength += cvcObjectService.LengthMeters(obj) ?? 0;
                    totalArea += cvcObjectService.InsulationAreaM2(obj) ?? 0;
                    totalWeight += cvcObjectService.WeightKg(obj) ?? 0;
                }
                else
                {
                    accessoryCounts[obj.Type] = accessoryCounts.GetValueOrDefault(obj.Type) + 1;
                }
            }
        }

        return new ProjectMetresSummary(
            projectId,
            projectDrawings.Count,
            totalLength,
            totalArea,
            totalWeight,
            accessoryCounts
                .Select(kv => new AccessoryCount(kv.Key, kv.Value))
                .OrderBy(a => a.Type)
                .ToList());
    }
}
