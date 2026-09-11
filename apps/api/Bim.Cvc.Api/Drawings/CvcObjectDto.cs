using Bim.Cvc.Domain;

namespace Bim.Cvc.Api.Drawings;

public sealed record CreateCvcObjectRequest(
    string Type,
    PointDto? Start,
    PointDto? End,
    double? WidthMm,
    double? HeightMm,
    double? DiameterMm,
    PointDto? Position,
    double? RotationRad);

public sealed record CvcObjectDto(
    Guid Id,
    Guid DrawingId,
    string Type,
    PointDto? Start,
    PointDto? End,
    double? WidthMm,
    double? HeightMm,
    double? DiameterMm,
    double? LengthMeters,
    PointDto? Position,
    double RotationRad,
    IReadOnlyList<Guid> ConnectedObjectIds)
{
    public static CvcObjectDto From(CvcObject obj, double? lengthMeters) => new(
        obj.Id,
        obj.DrawingId,
        obj.Type.ToString(),
        ToDto(obj.Start),
        ToDto(obj.End),
        obj.WidthMm,
        obj.HeightMm,
        obj.DiameterMm,
        lengthMeters,
        ToDto(obj.Position),
        obj.RotationRad,
        obj.ConnectedObjectIds);

    private static PointDto? ToDto(Point2D? p) => p is null ? null : new PointDto(p.X, p.Y);
}
