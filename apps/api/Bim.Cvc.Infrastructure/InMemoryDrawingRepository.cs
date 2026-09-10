using System.Collections.Concurrent;
using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure;

/// <summary>
/// Stand-in de développement pour la persistance PostgreSQL (module 4).
/// Les données ne survivent pas au redémarrage du processus.
/// </summary>
public sealed class InMemoryDrawingRepository : IDrawingRepository
{
    private readonly ConcurrentDictionary<Guid, Drawing> _drawings = new();

    public void Add(Drawing drawing) => _drawings[drawing.Id] = drawing;

    public Drawing? Get(Guid id) => _drawings.GetValueOrDefault(id);
}
