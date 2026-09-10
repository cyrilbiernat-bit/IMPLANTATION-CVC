using Bim.Cvc.Application;
using Microsoft.AspNetCore.Mvc;

namespace Bim.Cvc.Api.Drawings;

[ApiController]
[Route("api/v1/drawings")]
public sealed class DrawingsController(DrawingService drawingService, IDrawingFileStore fileStore, IDrawingRepository repository) : ControllerBase
{
    private const long MaxRequestBodySizeBytes = 100 * 1024 * 1024;

    /// <summary>
    /// Module 1 — enregistre un plan PDF importé par l'utilisateur.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxRequestBodySizeBytes)]
    public async Task<ActionResult<DrawingDto>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { message = "Aucun fichier reçu." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var drawing = await drawingService.ImportAsync(file.FileName, stream, ct);
            return CreatedAtAction(nameof(GetFile), new { id = drawing.Id }, DrawingDto.From(drawing, Request));
        }
        catch (InvalidDrawingException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Module 2 — fixe l'échelle réelle du plan à partir de deux points et
    /// d'une distance connue.
    /// </summary>
    [HttpPut("{id:guid}/calibration")]
    public ActionResult<DrawingDto> Calibrate(Guid id, CalibrateDrawingRequest request)
    {
        try
        {
            var drawing = drawingService.Calibrate(
                id,
                request.PageNumber,
                request.PointA.ToDomain(),
                request.PointB.ToDomain(),
                request.RealDistanceMeters);
            return Ok(DrawingDto.From(drawing, Request));
        }
        catch (DrawingNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidDrawingException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Sert le contenu binaire d'un plan précédemment importé, pour
    /// réaffichage côté client (fond de plan).
    /// </summary>
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> GetFile(Guid id, CancellationToken ct)
    {
        var drawing = repository.Get(id);
        if (drawing is null)
        {
            return NotFound();
        }

        var stream = await fileStore.OpenReadAsync(drawing.StoragePath, ct);
        return File(stream, "application/pdf", drawing.FileName);
    }
}
