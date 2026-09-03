namespace BimMep.Core.Routing;

public interface IPathFinder
{
    Path3D FindPath(RoutingGraph3D graph, RoutingNode start, RoutingNode end, RoutingConstraints constraints);
}

/// <summary>
/// A* — utilise pour le routage point a point (docs §2.2, §16 P1). L'heuristique est la distance
/// euclidienne (admissible sur une grille 6-connexe a poids uniforme), garantissant l'optimalite.
/// </summary>
public sealed class AStarPathFinder : IPathFinder
{
    public Path3D FindPath(RoutingGraph3D graph, RoutingNode start, RoutingNode end, RoutingConstraints constraints)
    {
        var openSet = new PriorityQueue<RoutingNode, double>();
        var cameFrom = new Dictionary<RoutingNode, RoutingNode>();
        var gScore = new Dictionary<RoutingNode, double> { [start] = 0 };

        openSet.Enqueue(start, Heuristic(start, end));
        var visited = new HashSet<RoutingNode>();

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            if (current == end)
                return ReconstructPath(cameFrom, current, gScore[current]);

            if (!visited.Add(current)) continue;

            foreach (var edge in graph.GetEdgesFrom(current))
            {
                double tentativeG = gScore[current] + edge.Weight;
                if (!gScore.TryGetValue(edge.To, out var existingG) || tentativeG < existingG)
                {
                    gScore[edge.To] = tentativeG;
                    cameFrom[edge.To] = current;
                    openSet.Enqueue(edge.To, tentativeG + Heuristic(edge.To, end));
                }
            }
        }

        return Path3D.Empty; // aucun chemin trouve (obstacles bloquants) — a signaler a l'ingenieur, pas de resultat partiel silencieux
    }

    private static double Heuristic(RoutingNode a, RoutingNode b) => a.Position.DistanceTo(b.Position);

    private static Path3D ReconstructPath(Dictionary<RoutingNode, RoutingNode> cameFrom, RoutingNode end, double totalCost)
    {
        var waypoints = new List<Geometry.Point3D> { end.Position };
        var current = end;
        while (cameFrom.TryGetValue(current, out var prev))
        {
            waypoints.Add(prev.Position);
            current = prev;
        }
        waypoints.Reverse();

        double length = 0;
        for (int i = 1; i < waypoints.Count; i++)
            length += waypoints[i - 1].DistanceTo(waypoints[i]);

        return new Path3D(waypoints, length, totalCost);
    }
}

/// <summary>
/// Dijkstra — utilise quand plusieurs destinations doivent etre atteintes depuis une meme source
/// (ex. distribution d'un reseau de diffuseurs depuis une CTA, docs §2.2 FindShortestTree) : un seul
/// parcours produit l'arbre des plus courts chemins vers toutes les cibles, plus efficace que N
/// appels A* independants.
/// </summary>
public sealed class DijkstraPathFinder : IPathFinder
{
    public Path3D FindPath(RoutingGraph3D graph, RoutingNode start, RoutingNode end, RoutingConstraints constraints)
    {
        var tree = ComputeShortestPathTree(graph, start);
        return tree.TryGetValue(end, out var path) ? path : Path3D.Empty;
    }

    /// <summary>Calcule l'arbre des plus courts chemins depuis <paramref name="start"/> vers tous les noeuds atteignables.</summary>
    public Dictionary<RoutingNode, Path3D> ComputeShortestPathTree(RoutingGraph3D graph, RoutingNode start)
    {
        var dist = new Dictionary<RoutingNode, double> { [start] = 0 };
        var cameFrom = new Dictionary<RoutingNode, RoutingNode>();
        var visited = new HashSet<RoutingNode>();
        var queue = new PriorityQueue<RoutingNode, double>();
        queue.Enqueue(start, 0);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current)) continue;

            foreach (var edge in graph.GetEdgesFrom(current))
            {
                double tentative = dist[current] + edge.Weight;
                if (!dist.TryGetValue(edge.To, out var existing) || tentative < existing)
                {
                    dist[edge.To] = tentative;
                    cameFrom[edge.To] = current;
                    queue.Enqueue(edge.To, tentative);
                }
            }
        }

        var result = new Dictionary<RoutingNode, Path3D>();
        foreach (var node in dist.Keys)
        {
            var waypoints = new List<Geometry.Point3D> { node.Position };
            var cursor = node;
            while (cameFrom.TryGetValue(cursor, out var prev))
            {
                waypoints.Add(prev.Position);
                cursor = prev;
            }
            waypoints.Reverse();
            result[node] = new Path3D(waypoints, dist[node], dist[node]);
        }
        return result;
    }
}
