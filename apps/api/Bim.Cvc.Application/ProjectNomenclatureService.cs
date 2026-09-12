using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public sealed record NomenclatureRow(
    Guid ObjectId,
    string DrawingFileName,
    string LayerName,
    CvcObjectType Type,
    double? WidthMm,
    double? HeightMm,
    double? DiameterMm,
    double? LengthMeters,
    double? DebitM3h,
    double? VitesseMs,
    double? PressionPa,
    double? WeightKg,
    double? InsulationAreaM2);

/// <summary>
/// Module 6 — nomenclature : la liste détaillée, ligne par ligne, de tous
/// les objets CVC posés sur les plans d'un projet (par opposition aux
/// métrés du module 5, qui n'en donnent que les totaux agrégés).
/// </summary>
public sealed class ProjectNomenclatureService(
    ProjectService projects,
    IDrawingRepository drawings,
    ICvcObjectRepository objects,
    CvcObjectService cvcObjectService,
    LayerService layerService)
{
    public IReadOnlyList<NomenclatureRow> Compute(Guid projectId)
    {
        _ = projects.Get(projectId); // lève ProjectNotFoundException si le projet n'existe pas.

        var rows = new List<NomenclatureRow>();
        foreach (var drawing in drawings.GetByProject(projectId))
        {
            var layerNames = layerService.GetByDrawing(drawing.Id).ToDictionary(l => l.Id, l => l.Name);

            foreach (var obj in objects.GetByDrawing(drawing.Id))
            {
                rows.Add(new NomenclatureRow(
                    obj.Id,
                    drawing.FileName,
                    layerNames.GetValueOrDefault(obj.LayerId, "—"),
                    obj.Type,
                    obj.WidthMm,
                    obj.HeightMm,
                    obj.DiameterMm,
                    cvcObjectService.LengthMeters(obj),
                    obj.DebitM3h,
                    obj.VitesseMs,
                    obj.PressionPa,
                    cvcObjectService.WeightKg(obj),
                    cvcObjectService.InsulationAreaM2(obj)));
            }
        }

        return rows;
    }
}
