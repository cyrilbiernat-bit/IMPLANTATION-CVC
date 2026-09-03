using BimMep.Core.Bim;
using BimMep.Core.Geometry;
using BimMep.Core.Mep;

namespace BimMep.Core.ClashDetection;

public enum ClashResolutionStrategy
{
    OffsetDuct,       // "Gaine VS Poutre -> Decalage automatique" (cahier des charges §5)
    OffsetPipe,
    AdjustSlope,      // reprise de pente sur reseau gravitaire (EU/EV/EP)
    ManualReview       // conflit trop complexe pour une proposition automatique
}

public sealed class ClashResolution
{
    public required ClashResolutionStrategy Strategy { get; init; }
    public required BimElement AffectedElement { get; init; }
    public Vector3D Offset { get; init; }
    public required bool RequiresRecompute { get; init; }
    public required string Rationale { get; init; }
}

/// <summary>
/// Propose puis applique une correction de conflit (docs §2.2, F-CLASH-04). L'application declenche
/// un recalcul complet du reseau affecte via le RecomputeScheduler (Core.Bim) — jamais un simple
/// deplacement geometrique isole : c'est ce recalcul en cascade qui garantit que les raccords et le
/// dimensionnement restent coherents apres correction (docs §5.4).
/// </summary>
public sealed class ClashResolver
{
    private readonly RecomputeScheduler _scheduler;

    public ClashResolver(RecomputeScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <summary>
    /// Exemple type du cahier des charges : "Gaine VS Poutre -> decalage automatique". La structure
    /// (poutre) est jugee non deplaçable ; c'est le reseau MEP qui cede, avec un decalage minimal
    /// suffisant pour degager la penetration plus une marge de securite.
    /// </summary>
    public ClashResolution ProposeResolution(Clash clash, double safetyMarginM = 0.02)
    {
        bool aIsStructural = IsStructural(clash.ElementA);
        bool bIsStructural = IsStructural(clash.ElementB);

        if (clash.Type == ClashType.Hard && aIsStructural != bIsStructural)
        {
            var mepElement = aIsStructural ? clash.ElementB : clash.ElementA;
            double offsetDistance = clash.PenetrationDepthM + safetyMarginM;

            var strategy = mepElement switch
            {
                MepDuct => ClashResolutionStrategy.OffsetDuct,
                MepPipe => ClashResolutionStrategy.OffsetPipe,
                _ => ClashResolutionStrategy.ManualReview
            };

            if (strategy == ClashResolutionStrategy.ManualReview)
            {
                return ManualReview(clash, "Element MEP non pris en charge par la resolution automatique.");
            }

            return new ClashResolution
            {
                Strategy = strategy,
                AffectedElement = mepElement,
                Offset = new Vector3D(0, 0, offsetDistance), // decalage vertical par defaut ; le moteur de production
                                                              // choisit l'axe libre le moins impactant (docs §15.1)
                RequiresRecompute = true,
                Rationale = $"Conflit dur avec un element structurel ({(aIsStructural ? clash.ElementA : clash.ElementB).GetIfcType()}) : " +
                            $"decalage de {offsetDistance * 1000:F0} mm propose sur '{mepElement.Name}'."
            };
        }

        if (clash.Type == ClashType.Clearance && (clash.ElementA is MepPipe || clash.ElementB is MepPipe))
        {
            var pipe = (clash.ElementA as MepPipe) ?? (MepPipe)clash.ElementB;
            return new ClashResolution
            {
                Strategy = ClashResolutionStrategy.AdjustSlope,
                AffectedElement = pipe,
                RequiresRecompute = true,
                Rationale = $"Degagement insuffisant sur reseau gravitaire '{pipe.Name}' : reprise de pente proposee."
            };
        }

        return ManualReview(clash, "Conflit sans strategie automatique connue (deux elements structurels, ou type non gere).");
    }

    /// <summary>
    /// Applique la resolution : deplace l'element affecte puis relance le recalcul en cascade
    /// (docs §5.4) sur l'ensemble des elements dependants avant de considerer le conflit resolu.
    /// </summary>
    public RecomputeReport ApplyResolution(ClashResolution resolution, IReadOnlyDictionary<Guid, IRecomputable> allElements)
    {
        if (resolution.Strategy == ClashResolutionStrategy.ManualReview)
            throw new InvalidOperationException("Une resolution 'ManualReview' ne peut pas etre appliquee automatiquement.");

        var current = resolution.AffectedElement.Placement;
        var newOrigin = current.Origin.Add(resolution.Offset);
        resolution.AffectedElement.Placement = new Transform3D(newOrigin, current.YawRadians);
        resolution.AffectedElement.MarkDirty();

        if (!resolution.RequiresRecompute)
            return new RecomputeReport(Array.Empty<Guid>(), Array.Empty<string>());

        return _scheduler.RunFrom(new IRecomputable[] { resolution.AffectedElement }, allElements);
    }

    private static bool IsStructural(BimElement element) =>
        element.GetIfcType() is "IfcBeam" or "IfcColumn" or "IfcSlab" or "IfcWall";

    private static ClashResolution ManualReview(Clash clash, string reason) => new()
    {
        Strategy = ClashResolutionStrategy.ManualReview,
        AffectedElement = clash.ElementA,
        RequiresRecompute = false,
        Rationale = reason
    };
}
