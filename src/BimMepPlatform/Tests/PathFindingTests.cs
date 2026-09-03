using BimMep.Core.Geometry;
using BimMep.Core.Routing;
using Xunit;

namespace BimMep.Tests;

public class AStarPathFinderTests
{
    [Fact]
    public void FindPath_NoObstacles_ReturnsStraightPathWithExpectedLength()
    {
        var graph = new RoutingGraph3D(cellSizeM: 1.0);
        graph.BuildGrid(new AxisAlignedBox(new Point3D(0, 0, 0), new Point3D(4, 0, 0)));

        var start = graph.NearestNode(new Point3D(0, 0, 0))!;
        var end = graph.NearestNode(new Point3D(4, 0, 0))!;

        var path = new AStarPathFinder().FindPath(graph, start, end, new RoutingConstraints());

        Assert.NotEmpty(path.Waypoints);
        Assert.Equal(4.0, path.TotalLength, precision: 6);
    }

    [Fact]
    public void FindPath_PartialWall_DetoursAroundObstacle()
    {
        var graph = new RoutingGraph3D(cellSizeM: 1.0);
        graph.BuildGrid(new AxisAlignedBox(new Point3D(0, 0, 0), new Point3D(2, 0, 2)));

        // Bloque uniquement le noeud (1,0,0) : le trajet direct (0,0,0)->(1,0,0)->(2,0,0) est coupe.
        graph.AddObstacle(new AxisAlignedBox(new Point3D(0.9, -0.1, -0.1), new Point3D(1.1, 0.1, 0.1)));

        var start = graph.NearestNode(new Point3D(0, 0, 0))!;
        var end = graph.NearestNode(new Point3D(2, 0, 0))!;

        var path = new AStarPathFinder().FindPath(graph, start, end, new RoutingConstraints());

        Assert.NotEmpty(path.Waypoints);
        Assert.True(path.TotalLength > 2.0, "Le trajet doit contourner l'obstacle et donc etre plus long que la ligne directe.");
        Assert.DoesNotContain(path.Waypoints, p => Math.Abs(p.X - 1.0) < 1e-9 && Math.Abs(p.Z) < 1e-9);
    }

    [Fact]
    public void FindPath_FullWallBlocksAllRoutes_ReturnsEmptyPath()
    {
        var graph = new RoutingGraph3D(cellSizeM: 1.0);
        graph.BuildGrid(new AxisAlignedBox(new Point3D(0, 0, 0), new Point3D(2, 2, 2)));

        // Bloque tout le plan x=1 (tous y, z) : aucun chemin 6-connexe ne peut passer de x=0 a x=2.
        graph.AddObstacle(new AxisAlignedBox(new Point3D(0.9, -1, -1), new Point3D(1.1, 3, 3)));

        var start = graph.NearestNode(new Point3D(0, 0, 0))!;
        var end = graph.NearestNode(new Point3D(2, 0, 0))!;

        var path = new AStarPathFinder().FindPath(graph, start, end, new RoutingConstraints());

        Assert.Empty(path.Waypoints);
    }
}

public class RoutingServiceTests
{
    [Fact]
    public void RouteSegment_NoPathAvailable_ThrowsInvalidOperationException()
    {
        var graph = new RoutingGraph3D(cellSizeM: 1.0);
        graph.BuildGrid(new AxisAlignedBox(new Point3D(0, 0, 0), new Point3D(2, 2, 2)));
        graph.AddObstacle(new AxisAlignedBox(new Point3D(0.9, -1, -1), new Point3D(1.1, 3, 3)));

        var service = new RoutingService();

        Assert.Throws<InvalidOperationException>(() =>
            service.RouteSegment(graph, new Point3D(0, 0, 0), new Point3D(2, 0, 0), new RoutingConstraints()));
    }

    [Fact]
    public void OptimizeForWeight_FiltersOutVariantsExceedingMaxPressureLoss_AndOrdersByWeight()
    {
        var graph = new RoutingGraph3D(cellSizeM: 1.0);
        graph.BuildGrid(new AxisAlignedBox(new Point3D(0, 0, 0), new Point3D(10, 0, 0)));
        var service = new RoutingService();

        var constraints = new RoutingConstraints { MaxPressureLossPa = 1000.0 };
        var variants = service.OptimizeForWeight(
            graph, new Point3D(0, 0, 0), new Point3D(10, 0, 0), constraints,
            designFlowM3H: 5000,
            candidateSections: new (double, double)[] { (0.3, 0.2), (0.6, 0.4), (1.0, 0.6) });

        Assert.NotEmpty(variants);
        // Une section plus grande pese plus lourd (plus de tole) mais genere moins de perte de charge.
        for (int i = 1; i < variants.Count; i++)
            Assert.True(variants[i].EstimatedWeightKg >= variants[i - 1].EstimatedWeightKg);

        Assert.All(variants, v => Assert.True(v.EstimatedPressureLossPa <= constraints.MaxPressureLossPa));
    }
}
