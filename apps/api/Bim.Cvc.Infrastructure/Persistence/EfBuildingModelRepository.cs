using Bim.Cvc.Application;
using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure.Persistence;

public sealed class EfBuildingModelRepository(BimCvcDbContext db) : IBuildingModelRepository
{
    public void Add(BuildingModel model) => db.BuildingModels.Add(model);

    public BuildingModel? GetByProject(Guid projectId) =>
        db.BuildingModels.FirstOrDefault(m => m.ProjectId == projectId);

    public bool Remove(Guid id)
    {
        var model = db.BuildingModels.Find(id);
        if (model is null) return false;
        db.BuildingModels.Remove(model);
        return true;
    }
}
