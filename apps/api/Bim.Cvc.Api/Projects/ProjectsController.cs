using Bim.Cvc.Api.Drawings;
using Bim.Cvc.Application;
using Microsoft.AspNetCore.Mvc;

namespace Bim.Cvc.Api.Projects;

/// <summary>Module 4 — projets, qui regroupent les plans importés (module 1) pour l'agrégation des métrés (module 5).</summary>
[ApiController]
[Route("api/v1/projects")]
public sealed class ProjectsController(
    ProjectService projectService,
    ProjectMetresService metresService,
    DrawingService drawingService,
    LayerService layerService,
    IDrawingRepository drawings) : ControllerBase
{
    private const long MaxRequestBodySizeBytes = 100 * 1024 * 1024;

    [HttpGet]
    public ActionResult<IReadOnlyList<ProjectDto>> List() =>
        Ok(projectService.GetAll().Select(ProjectDto.From).ToList());

    [HttpPost]
    public ActionResult<ProjectDto> Create(CreateProjectRequest request)
    {
        try
        {
            var project = projectService.Create(request.Name);
            return CreatedAtAction(nameof(Get), new { id = project.Id }, ProjectDto.From(project));
        }
        catch (InvalidProjectException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public ActionResult<ProjectDto> Get(Guid id)
    {
        try
        {
            return Ok(ProjectDto.From(projectService.Get(id)));
        }
        catch (ProjectNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Module 1 — enregistre un plan importé et rattaché à ce projet.</summary>
    [HttpPost("{id:guid}/drawings")]
    [RequestSizeLimit(MaxRequestBodySizeBytes)]
    public async Task<ActionResult<DrawingDto>> UploadDrawing(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { message = "Aucun fichier reçu." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var drawing = await drawingService.ImportAsync(id, file.FileName, stream, ct);
            layerService.CreateDefault(drawing.Id);
            return Created($"/api/v1/drawings/{drawing.Id}/file", DrawingDto.From(drawing, Request));
        }
        catch (ProjectNotFoundException)
        {
            return NotFound(new { message = "Projet introuvable." });
        }
        catch (InvalidDrawingException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/drawings")]
    public ActionResult<IReadOnlyList<DrawingDto>> ListDrawings(Guid id)
    {
        try
        {
            _ = projectService.Get(id);
        }
        catch (ProjectNotFoundException)
        {
            return NotFound();
        }

        return Ok(drawings.GetByProject(id).Select(d => DrawingDto.From(d, Request)).ToList());
    }

    /// <summary>Module 5 — métrés agrégés sur l'ensemble des plans du projet.</summary>
    [HttpGet("{id:guid}/metres")]
    public ActionResult<ProjectMetresDto> Metres(Guid id)
    {
        try
        {
            return Ok(ProjectMetresDto.From(metresService.Compute(id)));
        }
        catch (ProjectNotFoundException)
        {
            return NotFound();
        }
    }
}
