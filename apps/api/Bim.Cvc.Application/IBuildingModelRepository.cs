using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public interface IBuildingModelRepository
{
    void Add(BuildingModel model);

    BuildingModel? GetByProject(Guid projectId);

    bool Remove(Guid id);
}
