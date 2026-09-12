using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public sealed class InvalidDrawingException(string message) : Exception(message);

public sealed class DrawingService(
    IDrawingFileStore fileStore,
    IDrawingRepository repository,
    IEnumerable<IPlanFileParser> parsers,
    ProjectService projects)
{
    private const long MaxSizeBytes = 100 * 1024 * 1024; // 100 Mo — plan architecte scanné

    public async Task<Drawing> ImportAsync(Guid projectId, string fileName, Stream content, CancellationToken ct = default)
    {
        _ = projects.Get(projectId); // lève ProjectNotFoundException si le projet n'existe pas.
        var format = ResolveFormat(fileName);

        // Certains lecteurs (ACadSharp DxfReader/DwgReader) ferment le flux
        // qu'on leur passe une fois la lecture terminée. On bufferise donc
        // une bonne fois le contenu, puis on ouvre un flux indépendant par
        // consommateur (parseur, stockage) plutôt que de réutiliser le même.
        byte[] bytes;
        await using (var buffer = new MemoryStream())
        {
            await content.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
        }

        if ((long)bytes.Length is 0 or > MaxSizeBytes)
        {
            throw new InvalidDrawingException("Le fichier est vide ou dépasse la taille maximale (100 Mo).");
        }

        var parser = parsers.FirstOrDefault(p => p.CanParse(format))
            ?? throw new InvalidDrawingException($"Format {format} non pris en charge.");

        ParsedPlan parsed;
        try
        {
            using var parseStream = new MemoryStream(bytes, writable: false);
            parsed = parser.Parse(format, parseStream);
        }
        catch (Exception ex) when (ex is not InvalidDrawingException)
        {
            throw new InvalidDrawingException(
                $"Impossible de lire ce fichier {format} : {ex.Message}");
        }

        if (parsed.PageCount < 1)
        {
            throw new InvalidDrawingException("Le plan ne contient aucune page/vue exploitable.");
        }

        var id = Guid.NewGuid();
        using var saveStream = new MemoryStream(bytes, writable: false);
        var storagePath = await fileStore.SaveAsync(id, fileName, saveStream, ct);

        var drawing = new Drawing
        {
            Id = id,
            ProjectId = projectId,
            FileName = fileName,
            StoragePath = storagePath,
            NbPages = parsed.PageCount,
            Format = format,
            VectorEntities = parsed.Entities,
        };
        repository.Add(drawing);
        return drawing;
    }

    private static PlanFormat ResolveFormat(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => PlanFormat.Pdf,
        ".dxf" => PlanFormat.Dxf,
        ".dwg" => PlanFormat.Dwg,
        ".ifc" => throw new InvalidDrawingException(
            "Le format IFC sera pris en charge dans une prochaine itération. En attendant, exportez votre plan en DXF ou PDF."),
        ".rvt" => throw new InvalidDrawingException(
            "Le format natif Revit (.rvt) n'est pas pris en charge. Depuis Revit, exportez votre vue en DWG, DXF ou PDF (Fichier > Exporter > Formats CAO), déjà pris en charge ici."),
        _ => throw new InvalidDrawingException("Formats acceptés : PDF, DXF, DWG."),
    };

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
