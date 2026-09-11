using System.Collections.Concurrent;
using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure;

public sealed class InMemoryCvcObjectRepository : ICvcObjectRepository
{
    private readonly ConcurrentDictionary<Guid, CvcObject> _objects = new();

    public void Add(CvcObject cvcObject) => _objects[cvcObject.Id] = cvcObject;

    public CvcObject? Get(Guid id) => _objects.GetValueOrDefault(id);

    public IReadOnlyList<CvcObject> GetByDrawing(Guid drawingId) =>
        _objects.Values.Where(o => o.DrawingId == drawingId).ToList();

    public bool Remove(Guid id) => _objects.TryRemove(id, out _);
}
