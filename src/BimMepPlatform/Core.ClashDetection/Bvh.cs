using BimMep.Core.Bim;
using BimMep.Core.Geometry;

namespace BimMep.Core.ClashDetection;

/// <summary>Element candidat au clash detection : sa boite englobante + l'entite BIM porteuse.</summary>
public sealed record ClashCandidate(BimElement Element, AxisAlignedBox Bounds);

/// <summary>
/// Noeud d'une BVH (Bounding Volume Hierarchy) — support du pre-filtrage de clash a large echelle
/// avant le test geometrique fin (docs §15.6). Construction par partitionnement median recursif sur
/// l'axe le plus large (approche simple et efficace en pratique pour des scenes CAO, meme si une
/// implementation de production utilise un SAH — Surface Area Heuristic — plus fin).
/// </summary>
public sealed class BvhNode
{
    public AxisAlignedBox Bounds { get; init; }
    public BvhNode? Left { get; init; }
    public BvhNode? Right { get; init; }
    public ClashCandidate? Leaf { get; init; }

    public bool IsLeaf => Leaf is not null;
}

public sealed class BvhTree
{
    public BvhNode? Root { get; private set; }

    public void Build(IReadOnlyList<ClashCandidate> candidates)
    {
        Root = candidates.Count == 0 ? null : BuildRecursive(candidates);
    }

    private static BvhNode BuildRecursive(IReadOnlyList<ClashCandidate> candidates)
    {
        if (candidates.Count == 1)
        {
            return new BvhNode { Bounds = candidates[0].Bounds, Leaf = candidates[0] };
        }

        var overallBounds = candidates.Select(c => c.Bounds).Aggregate((a, b) => a.Union(b));

        double extentX = overallBounds.Max.X - overallBounds.Min.X;
        double extentY = overallBounds.Max.Y - overallBounds.Min.Y;
        double extentZ = overallBounds.Max.Z - overallBounds.Min.Z;

        var sorted = (extentX >= extentY && extentX >= extentZ)
            ? candidates.OrderBy(c => c.Bounds.Center.X).ToList()
            : (extentY >= extentZ)
                ? candidates.OrderBy(c => c.Bounds.Center.Y).ToList()
                : candidates.OrderBy(c => c.Bounds.Center.Z).ToList();

        int mid = sorted.Count / 2;
        var left = BuildRecursive(sorted.Take(mid).ToList());
        var right = BuildRecursive(sorted.Skip(mid).ToList());

        return new BvhNode { Bounds = left.Bounds.Union(right.Bounds), Left = left, Right = right };
    }

    /// <summary>
    /// Retourne toutes les paires de feuilles dont les boites englobantes se chevauchent, par
    /// auto-intersection recursive de l'arbre : deux sous-arbres dont les boites ne se chevauchent
    /// pas sont elagues sans etre descendus (c'est cet elagage, en O(log n) par branche ecartee, qui
    /// rend la BVH utile face a une comparaison naive en O(n^2) sur un grand modele — docs §15.6).
    /// </summary>
    public IEnumerable<(ClashCandidate A, ClashCandidate B)> FindOverlappingPairs()
    {
        var results = new List<(ClashCandidate, ClashCandidate)>();
        if (Root is not null) SelfIntersect(Root, results);
        return results;
    }

    private static void SelfIntersect(BvhNode node, List<(ClashCandidate, ClashCandidate)> results)
    {
        if (node.IsLeaf) return;
        if (node.Left is not null && node.Right is not null)
            CrossIntersect(node.Left, node.Right, results);
        if (node.Left is not null) SelfIntersect(node.Left, results);
        if (node.Right is not null) SelfIntersect(node.Right, results);
    }

    private static void CrossIntersect(BvhNode a, BvhNode b, List<(ClashCandidate, ClashCandidate)> results)
    {
        if (!a.Bounds.Intersects(b.Bounds)) return; // elagage : aucune feuille de a ne peut chevaucher une feuille de b

        if (a.IsLeaf && b.IsLeaf)
        {
            results.Add((a.Leaf!, b.Leaf!));
            return;
        }

        if (!a.IsLeaf && (b.IsLeaf || Volume(a.Bounds) >= Volume(b.Bounds)))
        {
            CrossIntersect(a.Left!, b, results);
            CrossIntersect(a.Right!, b, results);
        }
        else
        {
            CrossIntersect(a, b.Left!, results);
            CrossIntersect(a, b.Right!, results);
        }
    }

    private static double Volume(AxisAlignedBox box) =>
        (box.Max.X - box.Min.X) * (box.Max.Y - box.Min.Y) * (box.Max.Z - box.Min.Z);
}
