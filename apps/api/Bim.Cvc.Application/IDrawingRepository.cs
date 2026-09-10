using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public interface IDrawingRepository
{
    void Add(Drawing drawing);

    Drawing? Get(Guid id);
}
