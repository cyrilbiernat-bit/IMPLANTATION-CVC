using Bim.Cvc.Application;
using Microsoft.AspNetCore.Mvc;

namespace Bim.Cvc.Api.Drawings;

/// <summary>Module 3 — calques d'un plan : affichage et verrouillage.</summary>
[ApiController]
[Route("api/v1/drawings/{drawingId:guid}/layers")]
public sealed class LayersController(LayerService service) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<LayerDto>> List(Guid drawingId)
    {
        try
        {
            return Ok(service.GetByDrawing(drawingId).Select(LayerDto.From).ToList());
        }
        catch (DrawingNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost]
    public ActionResult<LayerDto> Create(Guid drawingId, CreateLayerRequest request)
    {
        try
        {
            return Ok(LayerDto.From(service.Create(drawingId, request.Name)));
        }
        catch (DrawingNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPatch("{layerId:guid}")]
    public ActionResult<LayerDto> Update(Guid drawingId, Guid layerId, UpdateLayerRequest request)
    {
        try
        {
            var layer = service.Update(drawingId, layerId, request.Name, request.Color, request.Visible, request.Locked);
            return Ok(LayerDto.From(layer));
        }
        catch (LayerNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{layerId:guid}")]
    public IActionResult Delete(Guid drawingId, Guid layerId)
    {
        try
        {
            service.Delete(drawingId, layerId);
            return NoContent();
        }
        catch (DrawingNotFoundException)
        {
            return NotFound();
        }
        catch (LayerNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidLayerException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
