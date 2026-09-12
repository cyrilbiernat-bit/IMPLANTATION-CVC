using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Application.Tests;

public sealed class LayerServiceTests
{
    private static (LayerService sut, FakeLayerRepository layers, FakeCvcObjectRepository objects, Guid drawingId) CreateSut()
    {
        var drawings = new FakeDrawingRepository();
        var drawing = new Drawing { ProjectId = Guid.NewGuid(), FileName = "plan.pdf", StoragePath = "memory://x", NbPages = 1 };
        drawings.Add(drawing);

        var layers = new FakeLayerRepository();
        var objects = new FakeCvcObjectRepository();
        var sut = new LayerService(drawings, layers, objects);
        return (sut, layers, objects, drawing.Id);
    }

    [Fact]
    public void CreateDefault_creates_a_visible_unlocked_layer_named_Calque_1()
    {
        var (sut, _, _, drawingId) = CreateSut();

        var layer = sut.CreateDefault(drawingId);

        Assert.Equal("Calque 1", layer.Name);
        Assert.True(layer.Visible);
        Assert.False(layer.Locked);
        Assert.Equal(0, layer.Order);
    }

    [Fact]
    public void Create_with_a_name_uses_it_and_increments_the_order()
    {
        var (sut, _, _, drawingId) = CreateSut();
        sut.CreateDefault(drawingId);

        var layer = sut.Create(drawingId, "Réseau soufflage");

        Assert.Equal("Réseau soufflage", layer.Name);
        Assert.Equal(1, layer.Order);
    }

    [Fact]
    public void Create_falls_back_to_a_generated_name_when_blank()
    {
        var (sut, _, _, drawingId) = CreateSut();
        sut.CreateDefault(drawingId);

        var layer = sut.Create(drawingId, "   ");

        Assert.Equal("Calque 2", layer.Name);
    }

    [Fact]
    public void Create_throws_when_the_drawing_does_not_exist()
    {
        var (sut, _, _, _) = CreateSut();

        Assert.Throws<DrawingNotFoundException>(() => sut.Create(Guid.NewGuid(), "x"));
    }

    [Fact]
    public void GetByDrawing_returns_layers_ordered_by_creation()
    {
        var (sut, _, _, drawingId) = CreateSut();
        var first = sut.CreateDefault(drawingId);
        var second = sut.Create(drawingId, "Calque 2");

        var result = sut.GetByDrawing(drawingId);

        Assert.Equal([first.Id, second.Id], result.Select(l => l.Id));
    }

    [Fact]
    public void Update_changes_only_the_provided_fields()
    {
        var (sut, _, _, drawingId) = CreateSut();
        var layer = sut.CreateDefault(drawingId);

        var updated = sut.Update(drawingId, layer.Id, name: "Réseau extraction", color: null, visible: false, locked: null);

        Assert.Equal("Réseau extraction", updated.Name);
        Assert.Equal("#0d9488", updated.Color); // inchangé
        Assert.False(updated.Visible);
        Assert.False(updated.Locked); // inchangé
    }

    [Fact]
    public void Update_can_lock_and_hide_a_layer()
    {
        var (sut, _, _, drawingId) = CreateSut();
        var layer = sut.CreateDefault(drawingId);

        var updated = sut.Update(drawingId, layer.Id, name: null, color: "#ff0000", visible: false, locked: true);

        Assert.Equal("#ff0000", updated.Color);
        Assert.False(updated.Visible);
        Assert.True(updated.Locked);
    }

    [Fact]
    public void Update_throws_for_a_layer_from_another_drawing()
    {
        var (sut, _, _, drawingId) = CreateSut();
        var (_, _, _, otherDrawingId) = CreateSut();
        var layer = sut.CreateDefault(otherDrawingId);

        Assert.Throws<LayerNotFoundException>(() => sut.Update(drawingId, layer.Id, "x", null, null, null));
    }

    [Fact]
    public void IsLocked_reflects_the_layer_state()
    {
        var (sut, _, _, drawingId) = CreateSut();
        var layer = sut.CreateDefault(drawingId);
        Assert.False(sut.IsLocked(layer.Id));

        sut.Update(drawingId, layer.Id, null, null, null, locked: true);

        Assert.True(sut.IsLocked(layer.Id));
    }

    [Fact]
    public void IsLocked_is_false_for_an_unknown_layer()
    {
        var (sut, _, _, _) = CreateSut();

        Assert.False(sut.IsLocked(Guid.NewGuid()));
    }

    [Fact]
    public void RequireUnlockedLayer_throws_for_an_unknown_layer()
    {
        var (sut, _, _, drawingId) = CreateSut();

        Assert.Throws<InvalidCvcObjectException>(() => sut.RequireUnlockedLayer(drawingId, Guid.NewGuid()));
    }

    [Fact]
    public void RequireUnlockedLayer_throws_for_a_layer_from_another_drawing()
    {
        var (sut, _, _, drawingId) = CreateSut();
        var (_, _, _, otherDrawingId) = CreateSut();
        var layer = sut.CreateDefault(otherDrawingId);

        Assert.Throws<InvalidCvcObjectException>(() => sut.RequireUnlockedLayer(drawingId, layer.Id));
    }

    [Fact]
    public void RequireUnlockedLayer_throws_for_a_locked_layer()
    {
        var (sut, _, _, drawingId) = CreateSut();
        var layer = sut.CreateDefault(drawingId);
        sut.Update(drawingId, layer.Id, null, null, null, locked: true);

        Assert.Throws<InvalidCvcObjectException>(() => sut.RequireUnlockedLayer(drawingId, layer.Id));
    }

    [Fact]
    public void Delete_reassigns_objects_to_the_first_remaining_layer()
    {
        var (sut, _, objects, drawingId) = CreateSut();
        var first = sut.CreateDefault(drawingId);
        var second = sut.Create(drawingId, "Calque 2");
        var obj = new CvcObject { DrawingId = drawingId, LayerId = second.Id, Type = CvcObjectType.Diffuseur, Position = new Point2D(0, 0) };
        objects.Add(obj);

        sut.Delete(drawingId, second.Id);

        Assert.Equal(first.Id, obj.LayerId);
        Assert.DoesNotContain(sut.GetByDrawing(drawingId), l => l.Id == second.Id);
    }

    [Fact]
    public void Delete_refuses_to_remove_the_last_layer_of_a_drawing()
    {
        var (sut, _, _, drawingId) = CreateSut();
        var layer = sut.CreateDefault(drawingId);

        Assert.Throws<InvalidLayerException>(() => sut.Delete(drawingId, layer.Id));
    }

    [Fact]
    public void Delete_throws_for_an_unknown_layer()
    {
        var (sut, _, _, drawingId) = CreateSut();
        sut.CreateDefault(drawingId);

        Assert.Throws<LayerNotFoundException>(() => sut.Delete(drawingId, Guid.NewGuid()));
    }

    [Fact]
    public void Delete_throws_when_the_drawing_does_not_exist()
    {
        var (sut, _, _, _) = CreateSut();

        Assert.Throws<DrawingNotFoundException>(() => sut.Delete(Guid.NewGuid(), Guid.NewGuid()));
    }

    private sealed class FakeDrawingRepository : IDrawingRepository
    {
        private readonly Dictionary<Guid, Drawing> _drawings = [];

        public void Add(Drawing drawing) => _drawings[drawing.Id] = drawing;

        public Drawing? Get(Guid id) => _drawings.GetValueOrDefault(id);

        public IReadOnlyList<Drawing> GetByProject(Guid projectId) =>
            _drawings.Values.Where(d => d.ProjectId == projectId).ToList();
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

    private sealed class FakeLayerRepository : ILayerRepository
    {
        private readonly Dictionary<Guid, Layer> _layers = [];

        public void Add(Layer layer) => _layers[layer.Id] = layer;

        public Layer? Get(Guid id) => _layers.GetValueOrDefault(id);

        public IReadOnlyList<Layer> GetByDrawing(Guid drawingId) =>
            _layers.Values.Where(l => l.DrawingId == drawingId).OrderBy(l => l.Order).ToList();

        public bool Remove(Guid id) => _layers.Remove(id);
    }
}
