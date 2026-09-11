using Bim.Cvc.Application;
using Bim.Cvc.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Bim.Cvc.Api.Drawings;

/// <summary>Module 3 — gaines, accessoires, terminaux et équipements posés sur un plan.</summary>
[ApiController]
[Route("api/v1/drawings/{drawingId:guid}/objects")]
public sealed class CvcObjectsController(CvcObjectService service) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<CvcObjectDto>> List(Guid drawingId)
    {
        try
        {
            var items = service.GetByDrawing(drawingId);
            return Ok(items.Select(ToDto).ToList());
        }
        catch (DrawingNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public ActionResult<CvcObjectDto> Create(Guid drawingId, CreateCvcObjectRequest request)
    {
        if (!Enum.TryParse<CvcObjectType>(request.Type, ignoreCase: true, out var type))
        {
            return BadRequest(new { message = $"Type d'objet inconnu : {request.Type}" });
        }

        try
        {
            CvcObject obj;
            if (CvcObject.IsDuct(type))
            {
                if (request.Start is null || request.End is null)
                {
                    return BadRequest(new { message = "Points de départ et d'arrivée requis pour une gaine." });
                }
                obj = service.AddDuct(
                    drawingId,
                    request.LayerId,
                    type,
                    new Point2D(request.Start.X, request.Start.Y),
                    new Point2D(request.End.X, request.End.Y),
                    request.WidthMm,
                    request.HeightMm,
                    request.DiameterMm,
                    request.DebitM3h,
                    request.VitesseMs,
                    request.PressionPa);
            }
            else
            {
                if (request.Position is null)
                {
                    return BadRequest(new { message = "Position requise pour cet objet." });
                }
                obj = service.AddPointObject(
                    drawingId,
                    request.LayerId,
                    type,
                    new Point2D(request.Position.X, request.Position.Y),
                    request.RotationRad ?? 0,
                    request.DebitM3h,
                    request.VitesseMs,
                    request.PressionPa);
            }

            return CreatedAtAction(nameof(List), new { drawingId }, ToDto(obj));
        }
        catch (DrawingNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidCvcObjectException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{objectId:guid}")]
    public IActionResult Delete(Guid drawingId, Guid objectId)
    {
        try
        {
            service.Remove(drawingId, objectId);
            return NoContent();
        }
        catch (DrawingNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidCvcObjectException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private CvcObjectDto ToDto(CvcObject obj) =>
        CvcObjectDto.From(obj, service.LengthMeters(obj), service.WeightKg(obj), service.InsulationAreaM2(obj));
}
