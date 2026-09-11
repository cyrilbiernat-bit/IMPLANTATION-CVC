using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public interface ICvcObjectRepository
{
    void Add(CvcObject cvcObject);

    CvcObject? Get(Guid id);

    IReadOnlyList<CvcObject> GetByDrawing(Guid drawingId);

    bool Remove(Guid id);
}
