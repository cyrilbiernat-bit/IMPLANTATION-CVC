using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure.Persistence;

public sealed class EfCvcObjectRepository(BimCvcDbContext db) : ICvcObjectRepository
{
    public void Add(CvcObject cvcObject) => db.CvcObjects.Add(cvcObject);

    public CvcObject? Get(Guid id) => db.CvcObjects.Find(id);

    public IReadOnlyList<CvcObject> GetByDrawing(Guid drawingId)
    {
        // Une requête SQL seule ignore les objets déjà ajoutés au suivi de
        // changements mais pas encore enregistrés — SaveChanges n'a lieu
        // qu'une fois par requête HTTP (voir SaveChangesMiddleware), donc un
        // objet créé plus tôt dans la même requête (ex. autorouting, qui en
        // crée plusieurs d'affilée) doit être complété avec ceux du suivi
        // local plutôt que d'être invisible tant que la requête n'est pas terminée.
        var fromDb = db.CvcObjects.Where(o => o.DrawingId == drawingId).ToList();
        var fromDbIds = fromDb.Select(o => o.Id).ToHashSet();
        var pendingLocal = db.CvcObjects.Local.Where(o => o.DrawingId == drawingId && !fromDbIds.Contains(o.Id));
        return [.. fromDb, .. pendingLocal];
    }

    public bool Remove(Guid id)
    {
        var obj = db.CvcObjects.Find(id);
        if (obj is null) return false;
        db.CvcObjects.Remove(obj);
        return true;
    }
}
