using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Application.Tests;

public sealed class CvcObjectServiceTests
{
    private static (CvcObjectService sut, FakeDrawingRepository drawings, Guid drawingId) CreateSut(bool calibrated = true)
    {
        var drawings = new FakeDrawingRepository();
        var drawing = new Drawing { FileName = "plan.pdf", StoragePath = "memory://x", NbPages = 1 };
        if (calibrated)
        {
            // 100 px pour 10 m -> 0,1 m/px.
            drawing.Calibration = Calibration.Create(1, new CalibrationPoint(0, 0), new CalibrationPoint(100, 0), 10);
        }
        drawings.Add(drawing);

        var sut = new CvcObjectService(drawings, new FakeCvcObjectRepository());
        return (sut, drawings, drawing.Id);
    }

    [Fact]
    public void AddDuct_stores_a_rectangular_duct_with_its_real_length()
    {
        var (sut, _, drawingId) = CreateSut();

        var duct = sut.AddDuct(
            drawingId, CvcObjectType.GaineRectangulaire,
            new Point2D(0, 0), new Point2D(50, 0),
            widthMm: 400, heightMm: 250, diameterMm: null);

        Assert.Equal(400, duct.WidthMm);
        Assert.Equal(250, duct.HeightMm);
        Assert.Null(duct.DiameterMm);
        // 50 px * 0,1 m/px = 5 m.
        Assert.Equal(5, sut.LengthMeters(duct)!.Value, precision: 6);
    }

    [Fact]
    public void AddDuct_stores_a_circular_duct()
    {
        var (sut, _, drawingId) = CreateSut();

        var duct = sut.AddDuct(
            drawingId, CvcObjectType.GaineCirculaire,
            new Point2D(0, 0), new Point2D(20, 0),
            widthMm: null, heightMm: null, diameterMm: 315);

        Assert.Equal(315, duct.DiameterMm);
        Assert.Null(duct.WidthMm);
    }

    [Fact]
    public void LengthMeters_is_null_when_the_drawing_is_not_calibrated()
    {
        var (sut, _, drawingId) = CreateSut(calibrated: false);

        var duct = sut.AddDuct(drawingId, CvcObjectType.GaineCirculaire, new Point2D(0, 0), new Point2D(20, 0), null, null, 200);

        Assert.Null(sut.LengthMeters(duct));
    }

    [Theory]
    [InlineData(null, 250d)]
    [InlineData(400d, null)]
    public void AddDuct_rejects_a_rectangular_duct_missing_a_dimension(double? width, double? height)
    {
        var (sut, _, drawingId) = CreateSut();

        Assert.Throws<InvalidCvcObjectException>(() =>
            sut.AddDuct(drawingId, CvcObjectType.GaineRectangulaire, new Point2D(0, 0), new Point2D(10, 0), width, height, null));
    }

    [Fact]
    public void AddDuct_rejects_a_circular_duct_without_a_diameter()
    {
        var (sut, _, drawingId) = CreateSut();

        Assert.Throws<InvalidCvcObjectException>(() =>
            sut.AddDuct(drawingId, CvcObjectType.GaineCirculaire, new Point2D(0, 0), new Point2D(10, 0), null, null, null));
    }

    [Fact]
    public void AddDuct_rejects_identical_start_and_end_points()
    {
        var (sut, _, drawingId) = CreateSut();

        Assert.Throws<InvalidCvcObjectException>(() =>
            sut.AddDuct(drawingId, CvcObjectType.GaineCirculaire, new Point2D(5, 5), new Point2D(5, 5), null, null, 200));
    }

    [Fact]
    public void AddDuct_rejects_a_non_duct_type()
    {
        var (sut, _, drawingId) = CreateSut();

        Assert.Throws<InvalidCvcObjectException>(() =>
            sut.AddDuct(drawingId, CvcObjectType.Coude, new Point2D(0, 0), new Point2D(10, 0), null, null, 200));
    }

    [Fact]
    public void AddPointObject_stores_an_accessory_at_a_position()
    {
        var (sut, _, drawingId) = CreateSut();

        var diffuseur = sut.AddPointObject(drawingId, CvcObjectType.Diffuseur, new Point2D(12, 34), rotationRad: 1.5);

        Assert.Equal(12, diffuseur.Position!.X);
        Assert.Equal(34, diffuseur.Position.Y);
        Assert.Equal(1.5, diffuseur.RotationRad);
    }

    [Fact]
    public void AddPointObject_rejects_a_duct_type()
    {
        var (sut, _, drawingId) = CreateSut();

        Assert.Throws<InvalidCvcObjectException>(() =>
            sut.AddPointObject(drawingId, CvcObjectType.GaineRectangulaire, new Point2D(0, 0), 0));
    }

    [Fact]
    public void Operations_throw_when_the_drawing_does_not_exist()
    {
        var (sut, _, _) = CreateSut();
        var unknownDrawingId = Guid.NewGuid();

        Assert.Throws<DrawingNotFoundException>(() =>
            sut.AddPointObject(unknownDrawingId, CvcObjectType.Bouche, new Point2D(0, 0), 0));
        Assert.Throws<DrawingNotFoundException>(() => sut.GetByDrawing(unknownDrawingId));
    }

    [Fact]
    public void Placing_a_duct_endpoint_near_an_existing_accessory_connects_them_both_ways()
    {
        var (sut, _, drawingId) = CreateSut();
        // Tolérance 0,15 m = 1,5 px à cette échelle (0,1 m/px).
        var diffuseur = sut.AddPointObject(drawingId, CvcObjectType.Diffuseur, new Point2D(50, 50), 0);

        var duct = sut.AddDuct(drawingId, CvcObjectType.GaineCirculaire, new Point2D(0, 0), new Point2D(50, 51), null, null, 200);

        Assert.Contains(diffuseur.Id, duct.ConnectedObjectIds);
        Assert.Contains(duct.Id, diffuseur.ConnectedObjectIds);
    }

    [Fact]
    public void Placing_objects_far_apart_does_not_connect_them()
    {
        var (sut, _, drawingId) = CreateSut();
        var diffuseur = sut.AddPointObject(drawingId, CvcObjectType.Diffuseur, new Point2D(50, 50), 0);

        var duct = sut.AddDuct(drawingId, CvcObjectType.GaineCirculaire, new Point2D(0, 0), new Point2D(20, 20), null, null, 200);

        Assert.DoesNotContain(diffuseur.Id, duct.ConnectedObjectIds);
        Assert.DoesNotContain(duct.Id, diffuseur.ConnectedObjectIds);
    }

    [Fact]
    public void Uncalibrated_drawings_skip_automatic_connection_detection()
    {
        var (sut, _, drawingId) = CreateSut(calibrated: false);
        var diffuseur = sut.AddPointObject(drawingId, CvcObjectType.Diffuseur, new Point2D(50, 50), 0);

        var duct = sut.AddDuct(drawingId, CvcObjectType.GaineCirculaire, new Point2D(0, 0), new Point2D(50, 50), null, null, 200);

        Assert.Empty(duct.ConnectedObjectIds);
        Assert.Empty(diffuseur.ConnectedObjectIds);
    }

    [Fact]
    public void Remove_deletes_the_object_and_clears_references_to_it()
    {
        var (sut, _, drawingId) = CreateSut();
        var diffuseur = sut.AddPointObject(drawingId, CvcObjectType.Diffuseur, new Point2D(50, 50), 0);
        var duct = sut.AddDuct(drawingId, CvcObjectType.GaineCirculaire, new Point2D(0, 0), new Point2D(50, 51), null, null, 200);
        Assert.Contains(diffuseur.Id, duct.ConnectedObjectIds);

        sut.Remove(drawingId, diffuseur.Id);

        var remaining = sut.GetByDrawing(drawingId);
        Assert.DoesNotContain(remaining, o => o.Id == diffuseur.Id);
        var reloadedDuct = Assert.Single(remaining);
        Assert.DoesNotContain(diffuseur.Id, reloadedDuct.ConnectedObjectIds);
    }

    [Fact]
    public void Remove_throws_for_an_object_that_does_not_belong_to_the_drawing()
    {
        var (sut, _, drawingId) = CreateSut();

        Assert.Throws<InvalidCvcObjectException>(() => sut.Remove(drawingId, Guid.NewGuid()));
    }

    private sealed class FakeDrawingRepository : IDrawingRepository
    {
        private readonly Dictionary<Guid, Drawing> _drawings = [];

        public void Add(Drawing drawing) => _drawings[drawing.Id] = drawing;

        public Drawing? Get(Guid id) => _drawings.GetValueOrDefault(id);
    }

    private sealed class FakeCvcObjectRepository : ICvcObjectRepository
    {
        private readonly Dictionary<Guid, CvcObject> _objects = [];

        public void Add(CvcObject cvcObject) => _objects[cvcObject.Id] = cvcObject;

        public CvcObject? Get(Guid id) => _objects.GetValueOrDefault(id);

        public IReadOnlyList<CvcObject> GetByDrawing(Guid drawingId) =>
            _objects.Values.Where(o => o.DrawingId == drawingId).ToList();

        public bool Remove(Guid id) => _objects.Remove(id);
    }
}
