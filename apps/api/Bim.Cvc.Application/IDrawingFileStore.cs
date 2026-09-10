namespace Bim.Cvc.Application;

/// <summary>
/// Persiste le contenu binaire d'un plan importé. Implémentation de
/// développement : disque local (voir Infrastructure). À remplacer par
/// Azure Blob Storage en production, sans changer cette interface.
/// </summary>
public interface IDrawingFileStore
{
    Task<string> SaveAsync(Guid drawingId, string fileName, Stream content, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string storagePath, CancellationToken ct = default);
}
