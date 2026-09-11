using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public interface ILayerRepository
{
    void Add(Layer layer);

    Layer? Get(Guid id);

    /// <summary>Calques du plan, triés par ordre d'affichage.</summary>
    IReadOnlyList<Layer> GetByDrawing(Guid drawingId);

    bool Remove(Guid id);
}
