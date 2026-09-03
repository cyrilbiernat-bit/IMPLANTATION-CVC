using BimMep.Core.Bim;
using BimMep.Core.Geometry;

namespace BimMep.Core.ClashDetection;

public enum ClashType { Hard, Soft, Clearance }
public enum ClashSeverity { Critical, Major, Minor }

public sealed class Clash
{
    public Guid Id { get; } = Guid.NewGuid();
    public required BimElement ElementA { get; init; }
    public required BimElement ElementB { get; init; }
    public required ClashType Type { get; init; }
    public required ClashSeverity Severity { get; init; }
    public required Point3D Location { get; init; }
    public double PenetrationDepthM { get; init; }
    public ClashResolution? SuggestedResolution { get; set; }
}

/// <summary>Fournit la boite englobante courante d'un element — abstrait la source reelle (cache
/// geometrique du moteur, cf. docs §4.2 colonne `bbox`) pour garder ce module testable isolement.</summary>
public interface IElementBoundsProvider
{
    AxisAlignedBox GetBounds(BimElement element);
}

/// <summary>
/// Regles de degagement minimal par paire de categories (docs F-CLASH-02). Cle = paire de
/// GetIfcType() triee alphabetiquement pour eviter de dupliquer chaque regle dans les deux sens.
/// </summary>
public sealed class ClearanceRules
{
    private readonly Dictionary<(string, string), double> _minClearanceM = new();

    public void SetRule(string ifcTypeA, string ifcTypeB, double minClearanceM)
    {
        var key = string.CompareOrdinal(ifcTypeA, ifcTypeB) <= 0 ? (ifcTypeA, ifcTypeB) : (ifcTypeB, ifcTypeA);
        _minClearanceM[key] = minClearanceM;
    }

    public double GetRule(string ifcTypeA, string ifcTypeB)
    {
        var key = string.CompareOrdinal(ifcTypeA, ifcTypeB) <= 0 ? (ifcTypeA, ifcTypeB) : (ifcTypeB, ifcTypeA);
        return _minClearanceM.TryGetValue(key, out var value) ? value : 0.0;
    }
}

/// <summary>
/// Detecte les interferences dures et les depassements de degagement entre elements du modele
/// (docs §2.2, F-CLASH-01/02/03). S'appuie sur la BVH pour ne comparer finement que les paires dont
/// les boites englobantes se chevauchent ou sont proches de moins que le degagement requis.
/// </summary>
public sealed class ClashDetector
{
    private readonly IElementBoundsProvider _boundsProvider;
    private readonly ClearanceRules _clearanceRules;

    public ClashDetector(IElementBoundsProvider boundsProvider, ClearanceRules? clearanceRules = null)
    {
        _boundsProvider = boundsProvider;
        _clearanceRules = clearanceRules ?? new ClearanceRules();
    }

    public BvhTree BuildBvh(IReadOnlyList<BimElement> elements)
    {
        var candidates = elements.Select(e => new ClashCandidate(e, _boundsProvider.GetBounds(e))).ToList();
        var tree = new BvhTree();
        tree.Build(candidates);
        return tree;
    }

    public IReadOnlyList<Clash> DetectClashes(IReadOnlyList<BimElement> elements)
    {
        var tree = BuildBvh(elements);
        var clashes = new List<Clash>();

        // Le seuil d'elargissement de la BVH doit couvrir la plus grande regle de degagement
        // configuree, sans quoi des paires "proches mais non chevauchantes" seraient elaguees a tort.
        double maxClearance = elements
            .SelectMany(a => elements.Where(b => b != a).Select(b => _clearanceRules.GetRule(a.GetIfcType(), b.GetIfcType())))
            .DefaultIfEmpty(0.0)
            .Max();

        var expandedCandidates = elements
            .Select(e => new ClashCandidate(e, _boundsProvider.GetBounds(e).ExpandedBy(maxClearance)))
            .ToList();
        var expandedTree = new BvhTree();
        expandedTree.Build(expandedCandidates);

        foreach (var (a, b) in expandedTree.FindOverlappingPairs())
        {
            var realBoundsA = _boundsProvider.GetBounds(a.Element);
            var realBoundsB = _boundsProvider.GetBounds(b.Element);

            if (realBoundsA.Intersects(realBoundsB))
            {
                double penetration = realBoundsA.PenetrationDepth(realBoundsB);
                clashes.Add(new Clash
                {
                    ElementA = a.Element,
                    ElementB = b.Element,
                    Type = ClashType.Hard,
                    Severity = ClassifySeverity(penetration),
                    Location = realBoundsA.Union(realBoundsB).Center,
                    PenetrationDepthM = penetration
                });
                continue;
            }

            double requiredClearance = _clearanceRules.GetRule(a.Element.GetIfcType(), b.Element.GetIfcType());
            if (requiredClearance <= 0) continue;

            double actualGap = DistanceBetween(realBoundsA, realBoundsB);
            if (actualGap < requiredClearance)
            {
                clashes.Add(new Clash
                {
                    ElementA = a.Element,
                    ElementB = b.Element,
                    Type = ClashType.Clearance,
                    Severity = ClashSeverity.Minor,
                    Location = realBoundsA.Union(realBoundsB).Center,
                    PenetrationDepthM = requiredClearance - actualGap
                });
            }
        }

        return clashes;
    }

    private static ClashSeverity ClassifySeverity(double penetrationM) => penetrationM switch
    {
        >= 0.10 => ClashSeverity.Critical,
        >= 0.02 => ClashSeverity.Major,
        _ => ClashSeverity.Minor
    };

    private static double DistanceBetween(AxisAlignedBox a, AxisAlignedBox b)
    {
        double dx = Math.Max(0, Math.Max(a.Min.X - b.Max.X, b.Min.X - a.Max.X));
        double dy = Math.Max(0, Math.Max(a.Min.Y - b.Max.Y, b.Min.Y - a.Max.Y));
        double dz = Math.Max(0, Math.Max(a.Min.Z - b.Max.Z, b.Min.Z - a.Max.Z));
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}
