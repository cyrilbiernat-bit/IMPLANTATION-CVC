using Bim.Cvc.Application;
using Microsoft.AspNetCore.Mvc;

namespace Bim.Cvc.Api.Drawings;

[ApiController]
[Route("api/v1/drawings")]
public sealed class DrawingsController(
    DrawingService drawingService,
    IDrawingFileStore fileStore,
    IDrawingRepository repository) : ControllerBase
{
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
    /// réaffichage côté client (fond de plan) ou téléchargement.
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
        var contentType = drawing.Format switch
        {
            Bim.Cvc.Domain.PlanFormat.Pdf => "application/pdf",
            Bim.Cvc.Domain.PlanFormat.Dxf => "image/vnd.dxf",
            Bim.Cvc.Domain.PlanFormat.Dwg => "application/acad",
            _ => "application/octet-stream",
        };
        return File(stream, contentType, drawing.FileName);
    }

    /// <summary>
    /// Géométrie 2D extraite d'un plan vectoriel (DXF/DWG) — tableau vide
    /// pour un PDF, qui n'a pas de représentation vectorielle.
    /// </summary>
    [HttpGet("{id:guid}/entities")]
    public ActionResult<IReadOnlyList<PlanEntityDto>> GetEntities(Guid id)
    {
        var drawing = repository.Get(id);
        if (drawing is null)
        {
            return NotFound();
        }

        var entities = drawing.VectorEntities?.Select(PlanEntityDto.From).ToList() ?? [];
        return Ok(entities);
    }
}
