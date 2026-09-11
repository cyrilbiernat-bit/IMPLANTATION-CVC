using Bim.Cvc.Domain;

namespace Bim.Cvc.Api.Drawings;

/// <summary>DTO plat (un seul type, champs optionnels) pour rester simple à sérialiser côté client.</summary>
public sealed record PlanEntityDto(
    string Type,
    PointDto[]? Points = null,
    bool? Closed = null,
    PointDto? Center = null,
    double? Radius = null,
    double? StartAngle = null,
    double? EndAngle = null,
    PointDto? Position = null,
    string? Text = null,
    double? Height = null)
{
    public static PlanEntityDto From(PlanEntity entity) => entity switch
    {
        PlanLine l => new PlanEntityDto("line", Points: [ToDto(l.Start), ToDto(l.End)]),
        PlanPolyline p => new PlanEntityDto("polyline", Points: [.. p.Points.Select(ToDto)], Closed: p.Closed),
        PlanCircle c => new PlanEntityDto("circle", Center: ToDto(c.Center), Radius: c.Radius),
        PlanArc a => new PlanEntityDto("arc", Center: ToDto(a.Center), Radius: a.Radius, StartAngle: a.StartAngleRad, EndAngle: a.EndAngleRad),
        PlanText t => new PlanEntityDto("text", Position: ToDto(t.Position), Text: t.Value, Height: t.Height),
        _ => throw new NotSupportedException($"Entité de plan non prise en charge : {entity.GetType().Name}"),
    };

    private static PointDto ToDto(Point2D p) => new(p.X, p.Y);
}
