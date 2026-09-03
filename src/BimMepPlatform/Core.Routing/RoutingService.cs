using BimMep.Core.Geometry;
using BimMep.Core.Mep;

namespace BimMep.Core.Routing;

public sealed record RoutingVariant(
    string Label,
    Path3D Path,
    double EstimatedWeightKg,
    double EstimatedPressureLossPa);

/// <summary>
/// Service de routage exploitant RoutingGraph3D + A*/Dijkstra pour proposer un trace entre deux
/// connecteurs (docs §2.2, F-ROUTE-01/02/04), et pour generer des variantes comparees a la demande
/// du copilote IA ("Optimise le reseau pour minimiser le poids des gaines", docs §7.3.1).
/// </summary>
public sealed class RoutingService
{
    private readonly IPathFinder _pathFinder;

    public RoutingService(IPathFinder? pathFinder = null)
    {
        _pathFinder = pathFinder ?? new AStarPathFinder();
    }

    /// <summary>Route un troncon unique entre deux points, en respectant les contraintes fournies (docs F-ROUTE-03).</summary>
    public Path3D RouteSegment(RoutingGraph3D graph, Point3D from, Point3D to, RoutingConstraints constraints)
    {
        var startNode = graph.NearestNode(from) ?? throw new InvalidOperationException("Point de depart hors du graphe de routage ou obstrue.");
        var endNode = graph.NearestNode(to) ?? throw new InvalidOperationException("Point d'arrivee hors du graphe de routage ou obstrue.");

        var path = _pathFinder.FindPath(graph, startNode, endNode, constraints);
        if (path.Waypoints.Count == 0)
            throw new InvalidOperationException("Aucun trace trouve respectant les contraintes et evitant les obstacles.");

        return path;
    }

    /// <summary>
    /// Genere plusieurs variantes de trace pour un meme couple origine/destination, chacune
    /// approximee par une section de gaine differente, et les chiffre (poids, perte de charge) pour
    /// permettre une comparaison a l'ingenieur (docs §7.3.1 — sortie du skill copilote "OptimizeNetwork").
    /// Le poids est estime a partir du perimetre de tole developpe (approximation rectangulaire),
    /// la perte de charge par le meme modele simplifie que MepNetwork.ComputeLosses.
    /// </summary>
    public IReadOnlyList<RoutingVariant> OptimizeForWeight(
        RoutingGraph3D graph, Point3D from, Point3D to, RoutingConstraints constraints,
        double designFlowM3H, IReadOnlyList<(double widthM, double heightM)> candidateSections,
        double steelSheetKgPerM2 = 6.0)
    {
        var basePath = RouteSegment(graph, from, to, constraints);
        var variants = new List<RoutingVariant>();

        foreach (var (widthM, heightM) in candidateSections)
        {
            double perimeterM = 2 * (widthM + heightM);
            double areaM2 = widthM * heightM;
            double weightKg = perimeterM * basePath.TotalLength * steelSheetKgPerM2;

            double flowM3S = designFlowM3H / 3600.0;
            double velocityMs = areaM2 > 0 ? flowM3S / areaM2 : double.PositiveInfinity;
            double hydraulicDiameterM = 2.0 * areaM2 / Math.Max(widthM + heightM, 1e-6);
            const double frictionFactor = 0.02;
            double pressureLossPa = frictionFactor * (basePath.TotalLength / Math.Max(hydraulicDiameterM, 1e-3))
                                     * (1.2 * velocityMs * velocityMs / 2.0);

            variants.Add(new RoutingVariant(
                Label: $"{widthM * 1000:F0}x{heightM * 1000:F0} mm",
                Path: basePath,
                EstimatedWeightKg: weightKg,
                EstimatedPressureLossPa: pressureLossPa));
        }

        return variants
            .Where(v => v.EstimatedPressureLossPa <= constraints.MaxPressureLossPa)
            .OrderBy(v => v.EstimatedWeightKg)
            .ToList();
    }

    /// <summary>Meme principe que OptimizeForWeight mais classe par perte de charge croissante (docs §16 P1/P2).</summary>
    public IReadOnlyList<RoutingVariant> OptimizeForPressureLoss(
        RoutingGraph3D graph, Point3D from, Point3D to, RoutingConstraints constraints,
        double designFlowM3H, IReadOnlyList<(double widthM, double heightM)> candidateSections) =>
        OptimizeForWeight(graph, from, to, constraints, designFlowM3H, candidateSections)
            .OrderBy(v => v.EstimatedPressureLossPa)
            .ToList();
}
