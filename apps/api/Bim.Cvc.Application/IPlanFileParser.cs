using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public sealed record ParsedPlan(int PageCount, IReadOnlyList<PlanEntity>? Entities);

/// <summary>
/// Lit un fichier de plan et en extrait ce qui est nécessaire à l'affichage
/// et à la calibration. Une implémentation par format (PDF, DXF/DWG...) ;
/// le format IFC et le format natif Revit (.rvt) n'ont volontairement pas
/// d'implémentation pour l'instant (voir <see cref="DrawingService"/>).
/// </summary>
public interface IPlanFileParser
{
    bool CanParse(PlanFormat format);

    ParsedPlan Parse(PlanFormat format, Stream content);
}
