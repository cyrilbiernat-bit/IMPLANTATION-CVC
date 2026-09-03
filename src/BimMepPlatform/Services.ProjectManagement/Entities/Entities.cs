using NetTopologySuite.Geometries;

namespace BimMep.Services.ProjectManagement.Entities;

/// <summary>
/// Entites EF Core mappant directement le schema relationnel documente (docs
/// 04-base-de-donnees.md §4.2) — deliberement distinctes des classes de domaine Core.Bim/Core.Mep
/// (docs §3.3 : les modules Core.* ne referencent jamais Services.*, donc la persistance ne peut pas
/// mapper directement les classes de domaine polymorphes). <see cref="Mapping.ProjectMapper"/> assure
/// la conversion dans les deux sens. Perimetre de cette premiere passe (docs §13-modules-critiques) :
/// organizations/projects/levels/rooms/bim_elements/mep_connectors/mep_networks/clashes/users/
/// families/family_types — manufacturers/manufacturer_products et element_revisions restent a ajouter.
/// </summary>
public sealed class OrganizationEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public List<ProjectEntity> Projects { get; set; } = new();
}

public sealed class UserEntity
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
}

public sealed class ProjectEntity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = null!;
    public string Phase { get; set; } = "APS";
    public short LodTarget { get; set; } = 100;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public OrganizationEntity Organization { get; set; } = null!;
    public List<LevelEntity> Levels { get; set; } = new();
    public List<BimElementEntity> Elements { get; set; } = new();
    public List<RoomEntity> Rooms { get; set; } = new();
    public List<MepNetworkEntity> Networks { get; set; } = new();
    public List<ClashEntity> Clashes { get; set; } = new();
}

public sealed class LevelEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = null!;
    public double ElevationM { get; set; }
    public double HeightM { get; set; }
    public int SortOrder { get; set; }

    public ProjectEntity Project { get; set; } = null!;
}

public sealed class FamilyEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Category { get; set; } = null!;

    public List<FamilyTypeEntity> Types { get; set; } = new();
}

public sealed class FamilyTypeEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = null!;

    /// <summary>Parametres de type serialises en JSON (docs §4.2 family_types.parameters JSONB).</summary>
    public string ParametersJson { get; set; } = "{}";

    public FamilyEntity Family { get; set; } = null!;
}

/// <summary>
/// Ligne generique pour tout element BIM/MEP (docs §4.2 bim_elements) : la specialisation
/// (dimensions de gaine, diametre de tuyauterie, ...) vit dans <see cref="ParametersJson"/>, pas dans
/// des colonnes dediees — c'est <see cref="Category"/> qui indique comment le mapper doit interpreter
/// ce JSON pour reconstruire l'objet de domaine Core.Mep correspondant.
/// </summary>
public sealed class BimElementEntity
{
    public Guid Id { get; set; }
    public string IfcGuid { get; set; } = null!;
    public Guid ProjectId { get; set; }
    public Guid? LevelId { get; set; }
    public Guid? FamilyTypeId { get; set; }
    public string Category { get; set; } = null!;
    public string? Name { get; set; }
    public short Lod { get; set; }
    public string ParametersJson { get; set; } = "{}";
    public Point? Placement { get; set; }
    public Polygon? Bbox { get; set; }
    public int RevisionNumber { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ProjectEntity Project { get; set; } = null!;
    public LevelEntity? Level { get; set; }
    public FamilyTypeEntity? FamilyType { get; set; }
    public List<MepConnectorEntity> Connectors { get; set; } = new();
}

public sealed class MepNetworkEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Kind { get; set; } = null!;
    public string Name { get; set; } = null!;
    public double? DesignFlow { get; set; }
    public double? DesignPressureLoss { get; set; }

    public ProjectEntity Project { get; set; } = null!;
    public List<MepConnectorEntity> Connectors { get; set; } = new();
}

public sealed class MepConnectorEntity
{
    public Guid Id { get; set; }
    public Guid ElementId { get; set; }
    public string ConnectorType { get; set; } = null!;
    public Point Position { get; set; } = null!;
    public double DirectionX { get; set; }
    public double DirectionY { get; set; }
    public double DirectionZ { get; set; }
    public double SizePrimary { get; set; }
    public double SizeSecondary { get; set; }
    public Guid? ConnectedToId { get; set; }
    public Guid? SystemId { get; set; }

    /// <summary>Classification de systeme (docs §5.6, ex. "SupplyAir") — a distinguer de SystemId,
    /// qui rattache le connecteur a un IfcRef vers mep_networks. Nomme ainsi pour eviter toute
    /// ambiguite avec l'espace de noms `System` du .NET (docs §15).</summary>
    public string SystemClassification { get; set; } = null!;

    public BimElementEntity Element { get; set; } = null!;
    public MepConnectorEntity? ConnectedTo { get; set; }
    public MepNetworkEntity? Network { get; set; }
}

public sealed class RoomEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid LevelId { get; set; }
    public string Name { get; set; } = null!;
    public Polygon Boundary { get; set; } = null!;
    public double? AreaM2 { get; set; }
    public double? VolumeM3 { get; set; }
    public double? HeatingLoadW { get; set; }
    public double? CoolingLoadW { get; set; }

    public ProjectEntity Project { get; set; } = null!;
    public LevelEntity Level { get; set; } = null!;
}

public sealed class ClashEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ElementAId { get; set; }
    public Guid ElementBId { get; set; }
    public string ClashType { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public Point? Location { get; set; }
    public string Status { get; set; } = "open";
    public string? SuggestedResolutionJson { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public ProjectEntity Project { get; set; } = null!;
    public BimElementEntity ElementA { get; set; } = null!;
    public BimElementEntity ElementB { get; set; } = null!;
}
