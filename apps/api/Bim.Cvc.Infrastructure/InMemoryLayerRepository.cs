using System.Collections.Concurrent;
using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure;

public sealed class InMemoryLayerRepository : ILayerRepository
{
    private readonly ConcurrentDictionary<Guid, Layer> _layers = new();

    public void Add(Layer layer) => _layers[layer.Id] = layer;

    public Layer? Get(Guid id) => _layers.GetValueOrDefault(id);

    public IReadOnlyList<Layer> GetByDrawing(Guid drawingId) =>
        _layers.Values.Where(l => l.DrawingId == drawingId).OrderBy(l => l.Order).ToList();

    public bool Remove(Guid id) => _layers.TryRemove(id, out _);
}
