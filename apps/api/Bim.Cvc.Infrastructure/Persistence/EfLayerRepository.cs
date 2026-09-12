using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure.Persistence;

public sealed class EfLayerRepository(BimCvcDbContext db) : ILayerRepository
{
    public void Add(Layer layer) => db.Layers.Add(layer);

    public Layer? Get(Guid id) => db.Layers.Find(id);

    public IReadOnlyList<Layer> GetByDrawing(Guid drawingId) =>
        [.. db.Layers.Where(l => l.DrawingId == drawingId).OrderBy(l => l.Order)];

    public bool Remove(Guid id)
    {
        var layer = db.Layers.Find(id);
        if (layer is null) return false;
        db.Layers.Remove(layer);
        return true;
    }
}
