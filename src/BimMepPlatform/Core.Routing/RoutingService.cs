using BimMep.Core.Calculations;
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
    /// Le poids est estime a partir du perimetre de tole developpe (approximation rectangulaire) ; la
    /// perte de charge delegue au meme calcul physique que MepNetwork.ComputeLosses
    /// (<see cref="AerauliqueCalculator"/>, Core.Calculations) pour eviter toute duplication de formule.
    /// </summary>
    public IReadOnlyList<RoutingVariant> OptimizeForWeight(
        RoutingGraph3D graph, Point3D from, Point3D to, RoutingConstraints constraints,
        double designFlowM3H, IReadOnlyList<(double widthM, double heightM)> candidateSections,
        double steelSheetKgPerM2 = MaterialConstants.GalvanizedSteelSheetKgPerM2)
    {
        var basePath = RouteSegment(graph, from, to, constraints);
        var variants = new List<RoutingVariant>();

        foreach (var (widthM, heightM) in candidateSections)
        {
            double perimeterM = 2 * (widthM + heightM);
            double areaM2 = widthM * heightM;
            if (areaM2 <= 0) continue;

            double weightKg = perimeterM * basePath.TotalLength * steelSheetKgPerM2;
            double hydraulicDiameterM = 2.0 * areaM2 / Math.Max(widthM + heightM, 1e-6);

            var loss = AerauliqueCalculator.Compute(new DuctSegmentInput(
                LengthM: basePath.TotalLength,
                CrossSectionAreaM2: areaM2,
                HydraulicDiameterM: hydraulicDiameterM,
                FlowRateM3H: designFlowM3H));

            variants.Add(new RoutingVariant(
                Label: $"{widthM * 1000:F0}x{heightM * 1000:F0} mm",
                Path: basePath,
                EstimatedWeightKg: weightKg,
                EstimatedPressureLossPa: loss.TotalLossPa));
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
