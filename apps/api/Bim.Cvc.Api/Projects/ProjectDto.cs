using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Api.Projects;

public sealed record ProjectDto(Guid Id, string Name, DateTimeOffset CreatedAt)
{
    public static ProjectDto From(Project project) => new(project.Id, project.Name, project.CreatedAt);
}

public sealed record CreateProjectRequest(string Name);

public sealed record AccessoryCountDto(string Type, int Count)
{
    public static AccessoryCountDto From(AccessoryCount count) => new(count.Type.ToString(), count.Count);
}

public sealed record ProjectMetresDto(
    Guid ProjectId,
    int DrawingCount,
    double TotalDuctLengthMeters,
    double TotalInsulationAreaM2,
    double TotalWeightKg,
    IReadOnlyList<AccessoryCountDto> AccessoryCounts)
{
    public static ProjectMetresDto From(ProjectMetresSummary summary) => new(
        summary.ProjectId,
        summary.DrawingCount,
        summary.TotalDuctLengthMeters,
        summary.TotalInsulationAreaM2,
        summary.TotalWeightKg,
        summary.AccessoryCounts.Select(AccessoryCountDto.From).ToList());
}

public sealed record NomenclatureRowDto(
    Guid ObjectId,
    string DrawingFileName,
    string LayerName,
    string Type,
    double? WidthMm,
    double? HeightMm,
    double? DiameterMm,
    double? LengthMeters,
    double? DebitM3h,
    double? VitesseMs,
    double? PressionPa,
    double? WeightKg,
    double? InsulationAreaM2)
{
    public static NomenclatureRowDto From(NomenclatureRow row) => new(
        row.ObjectId,
        row.DrawingFileName,
        row.LayerName,
        row.Type.ToString(),
        row.WidthMm,
        row.HeightMm,
        row.DiameterMm,
        row.LengthMeters,
        row.DebitM3h,
        row.VitesseMs,
        row.PressionPa,
        row.WeightKg,
        row.InsulationAreaM2);
}
