using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure.Persistence;

public sealed class EfDrawingRepository(BimCvcDbContext db) : IDrawingRepository
{
    public void Add(Drawing drawing) => db.Drawings.Add(drawing);

    public Drawing? Get(Guid id) => db.Drawings.Find(id);

    public IReadOnlyList<Drawing> GetByProject(Guid projectId) =>
        [.. db.Drawings.Where(d => d.ProjectId == projectId).OrderBy(d => d.UploadedAt)];
}
