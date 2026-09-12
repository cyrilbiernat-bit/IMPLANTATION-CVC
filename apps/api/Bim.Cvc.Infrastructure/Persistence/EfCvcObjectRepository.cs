using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure.Persistence;

public sealed class EfCvcObjectRepository(BimCvcDbContext db) : ICvcObjectRepository
{
    public void Add(CvcObject cvcObject) => db.CvcObjects.Add(cvcObject);

    public CvcObject? Get(Guid id) => db.CvcObjects.Find(id);

    public IReadOnlyList<CvcObject> GetByDrawing(Guid drawingId) =>
        [.. db.CvcObjects.Where(o => o.DrawingId == drawingId)];

    public bool Remove(Guid id)
    {
        var obj = db.CvcObjects.Find(id);
        if (obj is null) return false;
        db.CvcObjects.Remove(obj);
        return true;
    }
}
