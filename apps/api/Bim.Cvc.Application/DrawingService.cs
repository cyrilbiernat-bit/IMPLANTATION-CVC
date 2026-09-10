using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public sealed class InvalidDrawingException(string message) : Exception(message);

public sealed class DrawingService(
    IDrawingFileStore fileStore,
    IDrawingRepository repository,
    IPdfPageCounter pageCounter)
{
    private const long MaxSizeBytes = 100 * 1024 * 1024; // 100 Mo — plan architecte scanné

    public async Task<Drawing> ImportAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDrawingException("Seuls les fichiers PDF sont acceptés.");
        }
        if (content.Length is 0 or > MaxSizeBytes)
        {
            throw new InvalidDrawingException("Le fichier est vide ou dépasse la taille maximale (100 Mo).");
        }

        int nbPages = pageCounter.CountPages(content);
        if (nbPages < 1)
        {
            throw new InvalidDrawingException("Le PDF ne contient aucune page exploitable.");
        }

        content.Position = 0;
        var id = Guid.NewGuid();
        var storagePath = await fileStore.SaveAsync(id, fileName, content, ct);

        var drawing = new Drawing
        {
            Id = id,
            FileName = fileName,
            StoragePath = storagePath,
            NbPages = nbPages,
        };
        repository.Add(drawing);
        return drawing;
    }

    /// <summary>
    /// Module 2 — fixe l'échelle réelle d'un plan à partir de deux points
    /// cliqués sur une cote connue.
    /// </summary>
    public Drawing Calibrate(
        Guid drawingId,
        int pageNumber,
        CalibrationPoint pointA,
        CalibrationPoint pointB,
        double realDistanceMeters)
    {
        var drawing = repository.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);

        if (pageNumber < 1 || pageNumber > drawing.NbPages)
        {
            throw new InvalidDrawingException(
                $"La page {pageNumber} n'existe pas dans ce plan ({drawing.NbPages} page(s)).");
        }

        try
        {
            drawing.Calibration = Calibration.Create(pageNumber, pointA, pointB, realDistanceMeters);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            throw new InvalidDrawingException(ex.Message);
        }

        return drawing;
    }
}
