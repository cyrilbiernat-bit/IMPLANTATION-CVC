using BimMep.Core.Geometry;
using BimMep.Core.Mep;

namespace BimMep.Core.Routing;

public sealed class RoutingNode
{
    public required Point3D Position { get; init; }
    public bool IsObstacle { get; set; }

    /// <summary>Coordonnees entieres dans la grille du RoutingGraph3D qui l'a cree — evite une recherche
    /// lineaire pour retrouver les voisins (cf. RoutingGraph3D.GetEdgesFrom).</summary>
    internal (int X, int Y, int Z) GridKey { get; init; }
}

public sealed class RoutingEdge
{
    public required RoutingNode From { get; init; }
    public required RoutingNode To { get; init; }
    public required double Weight { get; init; }
    public double PressureLossFactor { get; init; } = 1.0;
}

/// <summary>
/// Graphe 3D discretise servant de support au routage (docs §2.2, §15.6). En production, la
/// grille est generee a partir de la scene BIM (voxelisation autour des obstacles structurels et
/// des reseaux existants) ; ici, `BuildGrid` fournit une grille reguliere suffisante pour illustrer
/// A*/Dijkstra sur un exemple.
/// </summary>
public sealed class RoutingGraph3D
{
    private readonly Dictionary<(int x, int y, int z), RoutingNode> _nodes = new();
    private readonly double _cellSizeM;

    public RoutingGraph3D(double cellSizeM)
    {
        _cellSizeM = cellSizeM;
    }

    public IReadOnlyCollection<RoutingNode> Nodes => _nodes.Values;

    public void BuildGrid(AxisAlignedBox bounds)
    {
        int nx = (int)Math.Ceiling((bounds.Max.X - bounds.Min.X) / _cellSizeM);
        int ny = (int)Math.Ceiling((bounds.Max.Y - bounds.Min.Y) / _cellSizeM);
        int nz = (int)Math.Ceiling((bounds.Max.Z - bounds.Min.Z) / _cellSizeM);

        for (int x = 0; x <= nx; x++)
        for (int y = 0; y <= ny; y++)
        for (int z = 0; z <= nz; z++)
        {
            var position = new Point3D(
                bounds.Min.X + x * _cellSizeM,
                bounds.Min.Y + y * _cellSizeM,
                bounds.Min.Z + z * _cellSizeM);
            _nodes[(x, y, z)] = new RoutingNode { Position = position, GridKey = (x, y, z) };
        }
    }

    /// <summary>Marque comme obstacles tous les noeuds de la grille contenus dans la boite donnee (docs F-ROUTE-01).</summary>
    public void AddObstacle(AxisAlignedBox obstacleBounds)
    {
        foreach (var node in _nodes.Values)
        {
            var p = node.Position;
            bool inside = p.X >= obstacleBounds.Min.X && p.X <= obstacleBounds.Max.X
                       && p.Y >= obstacleBounds.Min.Y && p.Y <= obstacleBounds.Max.Y
                       && p.Z >= obstacleBounds.Min.Z && p.Z <= obstacleBounds.Max.Z;
            if (inside) node.IsObstacle = true;
        }
    }

    public RoutingNode? NearestNode(Point3D point) =>
        _nodes.Values
            .Where(n => !n.IsObstacle)
            .OrderBy(n => n.Position.DistanceTo(point))
            .FirstOrDefault();

    /// <summary>Voisinage 6-connexe (haut/bas/N/S/E/O) — evite les diagonales pour rester conforme aux
    /// pratiques de pose (gaines/tuyauteries a angle droit, cf. docs F-ROUTE-03 rayons de courbure).</summary>
    public IEnumerable<RoutingEdge> GetEdgesFrom(RoutingNode node)
    {
        var (bx, by, bz) = node.GridKey;
        (int dx, int dy, int dz)[] directions =
        {
            (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)
        };

        foreach (var (dx, dy, dz) in directions)
        {
            if (!_nodes.TryGetValue((bx + dx, by + dy, bz + dz), out var neighbor)) continue;
            if (neighbor.IsObstacle) continue;

            yield return new RoutingEdge
            {
                From = node,
                To = neighbor,
                Weight = _cellSizeM
            };
        }
    }
}

/// <summary>Contraintes appliquees lors du routage (docs F-ROUTE-03).</summary>
public sealed class RoutingConstraints
{
    public double MaxSlopePercent { get; init; } = 100.0;
    public double MinClearanceM { get; init; } = 0.05;
    public HashSet<(SystemClassification, SystemClassification)> AllowedCrossings { get; init; } = new();
    public double MaxPressureLossPa { get; init; } = double.MaxValue;
}

public sealed record Path3D(IReadOnlyList<Point3D> Waypoints, double TotalLength, double TotalCost)
{
    public static Path3D Empty => new(Array.Empty<Point3D>(), 0, 0);
}
