using BimMep.Core.Bim;
using BimMep.Core.ClashDetection;
using BimMep.Core.Geometry;
using BimMep.Core.Mep;
using Xunit;

namespace BimMep.Tests;

internal sealed class StubElement : BimElement
{
    private readonly string _ifcType;
    public StubElement(string name, string ifcType = "IfcTestElement") : base(name) => _ifcType = ifcType;
    public override string GetIfcType() => _ifcType;
}

internal sealed class FixedBoundsProvider : IElementBoundsProvider
{
    private readonly Dictionary<Guid, AxisAlignedBox> _bounds = new();
    public void Set(BimElement element, AxisAlignedBox box) => _bounds[element.Id] = box;
    public AxisAlignedBox GetBounds(BimElement element) => _bounds[element.Id];
}

public class BvhTreeTests
{
    [Fact]
    public void FindOverlappingPairs_ReturnsOnlyTrulyOverlappingPairs()
    {
        var a = new StubElement("A");
        var b = new StubElement("B");
        var c = new StubElement("C");

        var candidates = new List<ClashCandidate>
        {
            new(a, new AxisAlignedBox(new Point3D(0, 0, 0), new Point3D(1, 1, 1))),
            new(b, new AxisAlignedBox(new Point3D(0.5, 0.5, 0.5), new Point3D(1.5, 1.5, 1.5))), // chevauche A
            new(c, new AxisAlignedBox(new Point3D(100, 100, 100), new Point3D(101, 101, 101)))  // isole
        };

        var tree = new BvhTree();
        tree.Build(candidates);
        var pairs = tree.FindOverlappingPairs().ToList();

        Assert.Single(pairs);
        var (x, y) = pairs[0];
        Assert.True((x.Element == a && y.Element == b) || (x.Element == b && y.Element == a));
    }

    [Fact]
    public void FindOverlappingPairs_EmptyInput_ReturnsNoPairs()
    {
        var tree = new BvhTree();
        tree.Build(Array.Empty<ClashCandidate>());

        Assert.Empty(tree.FindOverlappingPairs());
    }
}

public class ClashDetectorTests
{
    [Fact]
    public void DetectClashes_OverlappingElements_ReturnsHardClashWithCorrectPenetration()
    {
        var a = new StubElement("A");
        var b = new StubElement("B");
        var provider = new FixedBoundsProvider();
        provider.Set(a, new AxisAlignedBox(new Point3D(0, 0, 0), new Point3D(1, 1, 1)));
        provider.Set(b, new AxisAlignedBox(new Point3D(0.5, 0.5, 0.5), new Point3D(1.5, 1.5, 1.5)));

        var detector = new ClashDetector(provider);
        var clashes = detector.DetectClashes(new List<BimElement> { a, b });

        var clash = Assert.Single(clashes);
        Assert.Equal(ClashType.Hard, clash.Type);
        Assert.Equal(0.5, clash.PenetrationDepthM, precision: 6);
        Assert.Equal(ClashSeverity.Critical, clash.Severity); // 0.5 m >= seuil "Critical" (0.10 m)
    }

    [Fact]
    public void DetectClashes_GapBelowRequiredClearance_ReturnsClearanceClash()
    {
        var a = new StubElement("A", "IfcCableCarrierSegment");
        var b = new StubElement("B", "IfcPipeSegment");
        var provider = new FixedBoundsProvider();
        provider.Set(a, new AxisAlignedBox(new Point3D(0, 0, 0), new Point3D(1, 1, 1)));
        provider.Set(b, new AxisAlignedBox(new Point3D(1.05, 0, 0), new Point3D(2.05, 1, 1))); // ecart reel de 0.05 m

        var rules = new ClearanceRules();
        rules.SetRule(a.GetIfcType(), b.GetIfcType(), minClearanceM: 0.10);

        var detector = new ClashDetector(provider, rules);
        var clashes = detector.DetectClashes(new List<BimElement> { a, b });

        var clash = Assert.Single(clashes);
        Assert.Equal(ClashType.Clearance, clash.Type);
    }

    [Fact]
    public void DetectClashes_FarApartWithoutRule_ReturnsNoClash()
    {
        var a = new StubElement("A");
        var b = new StubElement("B");
        var provider = new FixedBoundsProvider();
        provider.Set(a, new AxisAlignedBox(new Point3D(0, 0, 0), new Point3D(1, 1, 1)));
        provider.Set(b, new AxisAlignedBox(new Point3D(10, 10, 10), new Point3D(11, 11, 11)));

        var detector = new ClashDetector(provider);
        var clashes = detector.DetectClashes(new List<BimElement> { a, b });

        Assert.Empty(clashes);
    }
}

public class ClashResolverTests
{
    private sealed class TestBeam : BimElement
    {
        public TestBeam(string name) : base(name) { }
        public override string GetIfcType() => "IfcBeam";
    }

    private static MepDuct CreateDuct()
    {
        var family = new Family { Name = "Gaine rectangulaire", Category = "duct" };
        var type = family.AddType("Generique");
        var duct = new MepDuct("Troncon en conflit", type, DuctShape.Rectangular, lengthM: 8.0);
        duct.ResizeRectangular(0.8, 0.4);
        return duct;
    }

    [Fact]
    public void ProposeResolution_DuctVsBeam_ProposesOffsetOnDuctOnly()
    {
        var beam = new TestBeam("Beam1");
        var duct = CreateDuct();

        var clash = new Clash
        {
            ElementA = beam,
            ElementB = duct,
            Type = ClashType.Hard,
            Severity = ClashSeverity.Major,
            Location = new Point3D(0, 0, 0),
            PenetrationDepthM = 0.10
        };

        var resolver = new ClashResolver(new RecomputeScheduler());
        var resolution = resolver.ProposeResolution(clash);

        Assert.Equal(ClashResolutionStrategy.OffsetDuct, resolution.Strategy);
        Assert.Same(duct, resolution.AffectedElement);
        Assert.True(resolution.RequiresRecompute);
    }

    [Fact]
    public void ApplyResolution_OffsetsElementAndRecomputes()
    {
        var beam = new TestBeam("Beam1");
        var duct = CreateDuct();
        double originalZ = duct.Placement.Origin.Z;

        var clash = new Clash
        {
            ElementA = beam,
            ElementB = duct,
            Type = ClashType.Hard,
            Severity = ClashSeverity.Major,
            Location = new Point3D(0, 0, 0),
            PenetrationDepthM = 0.10
        };

        var scheduler = new RecomputeScheduler();
        var resolver = new ClashResolver(scheduler);
        var resolution = resolver.ProposeResolution(clash);

        var allElements = new Dictionary<Guid, IRecomputable> { [duct.Id] = duct };
        var report = resolver.ApplyResolution(resolution, allElements);

        Assert.True(duct.Placement.Origin.Z > originalZ);
        Assert.Contains(duct.Id, report.RecomputedInOrder);
    }

    [Fact]
    public void ProposeResolution_TwoStructuralElements_ReturnsManualReview()
    {
        var beam1 = new TestBeam("Beam1");
        var beam2 = new TestBeam("Beam2");

        var clash = new Clash
        {
            ElementA = beam1,
            ElementB = beam2,
            Type = ClashType.Hard,
            Severity = ClashSeverity.Minor,
            Location = new Point3D(0, 0, 0),
            PenetrationDepthM = 0.01
        };

        var resolver = new ClashResolver(new RecomputeScheduler());
        var resolution = resolver.ProposeResolution(clash);

        Assert.Equal(ClashResolutionStrategy.ManualReview, resolution.Strategy);
        Assert.Throws<InvalidOperationException>(() =>
            resolver.ApplyResolution(resolution, new Dictionary<Guid, IRecomputable>()));
    }
}
