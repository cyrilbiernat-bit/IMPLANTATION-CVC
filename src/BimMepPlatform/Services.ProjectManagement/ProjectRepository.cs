using BimMep.Core.Bim;
using Microsoft.EntityFrameworkCore;

namespace BimMep.Services.ProjectManagement;

/// <summary>
/// Facade de persistance pour l'agregat Project (docs §4.5 — le projet et ses elements se
/// sauvegardent/rechargent comme un tout). S'appuie sur <see cref="ProjectMapper"/> pour la
/// conversion domaine ↔ entites EF Core.
/// </summary>
public sealed class ProjectRepository
{
    private readonly BimMepDbContext _db;

    public ProjectRepository(BimMepDbContext db)
    {
        _db = db;
    }

    public async Task SaveNewProjectAsync(Project project, Guid organizationId, Guid createdByUserId, CancellationToken ct = default)
    {
        var entity = ProjectMapper.ToEntity(project, organizationId, createdByUserId);
        _db.Projects.Add(entity);
        await _db.SaveChangesAsync(ct);

        // Seconde passe : deux connecteurs nouvellement inseres peuvent se referencer mutuellement
        // (ConnectTo bidirectionnel, docs ProjectMapper.ToBimElementEntity) — on ne peut fixer
        // ConnectedToId qu'une fois les deux lignes deja presentes en base.
        var links = ProjectMapper.CollectConnectorLinks(project).ToList();
        if (links.Count == 0) return;

        var connectorIds = links.Select(l => l.ConnectorId).ToHashSet();
        var trackedConnectors = await _db.MepConnectors
            .Where(c => connectorIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        foreach (var (connectorId, connectedToId) in links)
            trackedConnectors[connectorId].ConnectedToId = connectedToId;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Recharge un projet complet (niveaux + elements) depuis la base. Ne restaure pas les
    /// connexions entre connecteurs ni les Family/FamilyType (docs ProjectMapper, limitation assumee
    /// de cette premiere passe).
    /// </summary>
    public async Task<Project?> LoadProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        var entity = await _db.Projects
            .Include(p => p.Levels)
            .Include(p => p.Elements).ThenInclude(e => e.Connectors)
            .Include(p => p.Rooms)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct);

        if (entity is null) return null;

        var project = new Project { Name = entity.Name };
        if (Enum.TryParse<ProjectPhase>(entity.Phase, out var phase))
            project.Phase = phase;
        project.CurrentLod = entity.LodTarget;

        var levelsById = new Dictionary<Guid, Level>();
        foreach (var levelEntity in entity.Levels)
        {
            var level = new Level
            {
                Name = levelEntity.Name,
                ElevationMeters = levelEntity.ElevationM,
                HeightMeters = levelEntity.HeightM,
                SortOrder = levelEntity.SortOrder,
            };
            project.Levels.Add(level);
            levelsById[levelEntity.Id] = level;
        }

        foreach (var elementEntity in entity.Elements)
        {
            var element = ProjectMapper.FromEntity(elementEntity);
            if (element is null) continue;

            if (elementEntity.LevelId is { } levelId && levelsById.TryGetValue(levelId, out var level))
                element.Level = level;

            project.Elements.Add(element);
        }

        foreach (var roomEntity in entity.Rooms)
        {
            if (!levelsById.TryGetValue(roomEntity.LevelId, out var roomLevel)) continue;
            project.Rooms.Add(ProjectMapper.FromRoomEntity(roomEntity, roomLevel));
        }

        return project;
    }
}
