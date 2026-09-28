using Bim.Cvc.Domain;

namespace Bim.Cvc.Api.Projects;

public sealed record BuildingElementDto(string IfcType, string Name, IReadOnlyList<float> Positions, IReadOnlyList<int> Indices)
{
    public static BuildingElementDto From(BuildingElement element) =>
        new(element.IfcType, element.Name, element.Positions, element.Indices);
}

public sealed record BuildingModelDto(
    Guid Id,
    Guid ProjectId,
    string FileName,
    DateTimeOffset UploadedAt,
    IReadOnlyList<BuildingElementDto> Elements,
    double OffsetXMeters,
    double OffsetZMeters)
{
    public static BuildingModelDto From(BuildingModel model) => new(
        model.Id,
        model.ProjectId,
        model.FileName,
        model.UploadedAt,
        model.Elements.Select(BuildingElementDto.From).ToList(),
        model.OffsetXMeters,
        model.OffsetZMeters);
}

public sealed record SetBuildingModelAlignmentRequest(double OffsetXMeters, double OffsetZMeters);
