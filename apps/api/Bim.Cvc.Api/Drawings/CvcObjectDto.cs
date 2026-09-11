using Bim.Cvc.Domain;

namespace Bim.Cvc.Api.Drawings;

public sealed record CreateCvcObjectRequest(
    string Type,
    Guid LayerId,
    PointDto? Start,
    PointDto? End,
    double? WidthMm,
    double? HeightMm,
    double? DiameterMm,
    PointDto? Position,
    double? RotationRad,
    double? DebitM3h,
    double? VitesseMs,
    double? PressionPa);

public sealed record CvcObjectDto(
    Guid Id,
    Guid DrawingId,
    Guid LayerId,
    string Type,
    PointDto? Start,
    PointDto? End,
    double? WidthMm,
    double? HeightMm,
    double? DiameterMm,
    double? LengthMeters,
    double? WeightKg,
    double? InsulationAreaM2,
    double? DebitM3h,
    double? VitesseMs,
    double? PressionPa,
    PointDto? Position,
    double RotationRad,
    IReadOnlyList<Guid> ConnectedObjectIds)
{
    public static CvcObjectDto From(CvcObject obj, double? lengthMeters, double? weightKg, double? insulationAreaM2) => new(
        obj.Id,
        obj.DrawingId,
        obj.LayerId,
        obj.Type.ToString(),
        ToDto(obj.Start),
        ToDto(obj.End),
        obj.WidthMm,
        obj.HeightMm,
        obj.DiameterMm,
        lengthMeters,
        weightKg,
        insulationAreaM2,
        obj.DebitM3h,
        obj.VitesseMs,
        obj.PressionPa,
        ToDto(obj.Position),
        obj.RotationRad,
        obj.ConnectedObjectIds);

    private static PointDto? ToDto(Point2D? p) => p is null ? null : new PointDto(p.X, p.Y);
}
