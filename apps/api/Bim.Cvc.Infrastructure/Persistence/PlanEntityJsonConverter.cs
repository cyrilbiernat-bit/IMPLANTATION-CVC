using System.Text.Json;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure.Persistence;

/// <summary>Représentation JSON plate d'une <see cref="PlanEntity"/>, pour la colonne jsonb "vector_entities".</summary>
internal sealed record PlanEntityJson(
    string Type,
    PointJson[]? Points = null,
    bool? Closed = null,
    PointJson? Center = null,
    double? Radius = null,
    double? StartAngleRad = null,
    double? EndAngleRad = null,
    PointJson? Position = null,
    string? Value = null,
    double? Height = null);

internal sealed record PointJson(double X, double Y);

/// <summary>
/// (Dé)sérialisation manuelle de <see cref="PlanEntity"/> — une hiérarchie
/// polymorphe — en JSON plat, pour éviter de faire dépendre le domaine
/// d'attributs de sérialisation.
/// </summary>
internal static class PlanEntityJsonConverter
{
    public static string? Serialize(IReadOnlyList<PlanEntity>? entities) =>
        entities is null ? null : JsonSerializer.Serialize(entities.Select(ToJson).ToList());

    public static List<PlanEntity>? Deserialize(string? json) =>
        json is null ? null : (JsonSerializer.Deserialize<List<PlanEntityJson>>(json) ?? []).Select(FromJson).ToList();

    private static PlanEntityJson ToJson(PlanEntity entity) => entity switch
    {
        PlanLine l => new PlanEntityJson("line", Points: [ToJson(l.Start), ToJson(l.End)]),
        PlanPolyline p => new PlanEntityJson("polyline", Points: [.. p.Points.Select(ToJson)], Closed: p.Closed),
        PlanCircle c => new PlanEntityJson("circle", Center: ToJson(c.Center), Radius: c.Radius),
        PlanArc a => new PlanEntityJson("arc", Center: ToJson(a.Center), Radius: a.Radius, StartAngleRad: a.StartAngleRad, EndAngleRad: a.EndAngleRad),
        PlanText t => new PlanEntityJson("text", Position: ToJson(t.Position), Value: t.Value, Height: t.Height),
        _ => throw new NotSupportedException($"Entité de plan non prise en charge : {entity.GetType().Name}"),
    };

    private static PlanEntity FromJson(PlanEntityJson j) => j.Type switch
    {
        "line" => new PlanLine(FromJson(j.Points![0]), FromJson(j.Points[1])),
        "polyline" => new PlanPolyline([.. j.Points!.Select(FromJson)], j.Closed ?? false),
        "circle" => new PlanCircle(FromJson(j.Center!), j.Radius!.Value),
        "arc" => new PlanArc(FromJson(j.Center!), j.Radius!.Value, j.StartAngleRad!.Value, j.EndAngleRad!.Value),
        "text" => new PlanText(FromJson(j.Position!), j.Value!, j.Height!.Value),
        _ => throw new NotSupportedException($"Type d'entité de plan inconnu en base : {j.Type}"),
    };

    private static PointJson ToJson(Point2D p) => new(p.X, p.Y);

    private static Point2D FromJson(PointJson p) => new(p.X, p.Y);
}
