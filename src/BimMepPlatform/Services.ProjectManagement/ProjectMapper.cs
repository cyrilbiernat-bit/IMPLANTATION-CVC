using System.Text.Json;
using BimMep.Core.Bim;
using BimMep.Core.Geometry;
using BimMep.Core.Mep;
using BimMep.Services.ProjectManagement.Entities;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace BimMep.Services.ProjectManagement;

/// <summary>
/// Convertit entre le modele de domaine en memoire (Core.Bim/Core.Mep) et les entites de persistance
/// (docs §3.3 : Core.* ignore Services.*, c'est donc cette couche qui connait les deux mondes).
///
/// Perimetre de cette premiere passe (docs §13-modules-critiques) : Family/FamilyType ne sont pas
/// round-trippes (FamilyTypeId reste null a la sauvegarde, les elements relus n'ont pas de FamilyType
/// associe) — chaque categorie MEP porte deja ses parametres geometriques dans <c>ParametersJson</c>,
/// qui suffit a reconstruire un objet de domaine fonctionnellement equivalent.
/// </summary>
public static class ProjectMapper
{
    private static readonly GeometryFactory Geometry = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 0);

    /// <summary>
    /// Enumere les paires (connecteur, connecteur cible) a appliquer en seconde passe apres l'insertion
    /// initiale (docs ProjectRepository — evite la dependance circulaire entre connecteurs mutuellement
    /// lies). Ne couvre que les categories dont les connecteurs sont exposes publiquement.
    /// </summary>
    public static IEnumerable<(Guid ConnectorId, Guid ConnectedToId)> CollectConnectorLinks(Project project)
    {
        IEnumerable<MepConnector> AllConnectors(BimElement element) => element switch
        {
            MepDuct duct => duct.Connectors,
            MepPipe pipe => pipe.Connectors,
            MepEquipment equipment => equipment.Connectors,
            _ => Enumerable.Empty<MepConnector>()
        };

        foreach (var element in project.Elements)
        foreach (var connector in AllConnectors(element))
        {
            if (connector.ConnectedTo is { } target)
                yield return (connector.Id, target.Id);
        }
    }

    public static ProjectEntity ToEntity(Project project, Guid organizationId, Guid createdByUserId)
    {
        var now = DateTimeOffset.UtcNow;
        var levelIdByDomainId = new Dictionary<Guid, Guid>();

        var entity = new ProjectEntity
        {
            Id = project.Id,
            OrganizationId = organizationId,
            Name = project.Name,
            Phase = project.Phase.ToString(),
            LodTarget = (short)project.CurrentLod,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var level in project.Levels)
        {
            levelIdByDomainId[level.Id] = level.Id;
            entity.Levels.Add(new LevelEntity
            {
                Id = level.Id,
                ProjectId = project.Id,
                Name = level.Name,
                ElevationM = level.ElevationMeters,
                HeightM = level.HeightMeters,
                SortOrder = level.SortOrder,
            });
        }

        foreach (var room in project.Rooms.Where(r => r.Boundary.Count >= 3))
        {
            entity.Rooms.Add(new RoomEntity
            {
                Id = room.Id,
                ProjectId = project.Id,
                LevelId = room.Level.Id,
                Name = room.Name,
                Boundary = ToPolygon(room.Boundary),
                AreaM2 = room.AreaM2,
                VolumeM3 = room.VolumeM3,
                HeatingLoadW = room.HeatingLoadW,
                CoolingLoadW = room.CoolingLoadW,
            });
        }

        foreach (var element in project.Elements)
        {
            var elementEntity = ToBimElementEntity(element, project.Id, createdByUserId, now);
            if (elementEntity is null) continue; // categorie hors perimetre (docs §13, meme regle que IfcProjectExporter)
            entity.Elements.Add(elementEntity);
        }

        return entity;
    }

    private static BimElementEntity? ToBimElementEntity(BimElement element, Guid projectId, Guid createdBy, DateTimeOffset now)
    {
        string category;
        object parameters;
        IEnumerable<MepConnector> connectors;

        switch (element)
        {
            case MepDuct duct:
                category = "MepDuct";
                parameters = new
                {
                    shape = duct.Shape.ToString(),
                    widthM = duct.WidthM,
                    heightM = duct.HeightM,
                    diameterM = duct.DiameterM,
                    lengthM = duct.LengthM,
                    material = duct.Material,
                    insulationThicknessM = duct.InsulationThicknessM,
                };
                connectors = duct.Connectors;
                break;

            case MepPipe pipe:
                category = "MepPipe";
                parameters = new
                {
                    diameterNominalM = pipe.DiameterNominalM,
                    systemType = pipe.SystemType.ToString(),
                    material = pipe.Material,
                    slopePercent = pipe.SlopePercent,
                    lengthM = pipe.LengthM,
                };
                connectors = pipe.Connectors;
                break;

            case CableTray tray:
                category = "CableTray";
                parameters = new
                {
                    widthM = tray.WidthM,
                    heightM = tray.HeightM,
                    lengthM = tray.LengthM,
                    trayType = tray.TrayType,
                };
                connectors = Enumerable.Empty<MepConnector>();
                break;

            case MepEquipment equipment:
                category = "MepEquipment";
                parameters = new { manufacturerReference = equipment.ManufacturerReference };
                connectors = equipment.Connectors;
                break;

            default:
                return null; // categorie hors perimetre (docs §13, meme regle que IfcProjectExporter)
        }

        var entity = new BimElementEntity
        {
            Id = element.Id,
            IfcGuid = element.IfcGuid,
            ProjectId = projectId,
            LevelId = element.Level?.Id,
            Category = category,
            Name = element.Name,
            Lod = (short)element.Lod,
            ParametersJson = JsonSerializer.Serialize(parameters),
            Placement = ToPoint(element.Placement.Origin),
            RevisionNumber = element.RevisionNumber,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now,
        };

        foreach (var connector in connectors)
        {
            entity.Connectors.Add(new MepConnectorEntity
            {
                Id = connector.Id,
                ElementId = element.Id,
                ConnectorType = connector.Type.ToString(),
                Position = ToPoint(connector.Position),
                DirectionX = connector.Direction.X,
                DirectionY = connector.Direction.Y,
                DirectionZ = connector.Direction.Z,
                SizePrimary = connector.SizePrimary,
                SizeSecondary = connector.SizeSecondary,
                // ConnectedToId est intentionnellement omis ici (docs ProjectRepository) : deux
                // connecteurs nouvellement crees peuvent se referencer mutuellement (ConnectTo est
                // bidirectionnel, Core.Mep §MepConnector), ce qui produirait une dependance circulaire
                // a l'insertion. Le repository le renseigne dans une seconde passe apres le premier
                // SaveChanges, une fois toutes les lignes existantes en base.
                SystemClassification = connector.System.ToString(),
            });
        }

        return entity;
    }

    /// <summary>
    /// Reconstruit un element de domaine a partir d'une ligne persistee. Ne restaure pas les
    /// connexions entre connecteurs (ConnectedTo) ni le FamilyType (docs limitation ci-dessus) :
    /// suffisant pour verifier que la geometrie/les parametres survivent a un aller-retour base.
    /// </summary>
    public static BimElement? FromEntity(BimElementEntity entity)
    {
        using var doc = JsonDocument.Parse(entity.ParametersJson);
        var root = doc.RootElement;

        BimElement? element = entity.Category switch
        {
            "MepDuct" => FromDuct(entity, root),
            "MepPipe" => FromPipe(entity, root),
            "CableTray" => FromCableTray(entity, root),
            "MepEquipment" => new MepEquipment(entity.Name ?? "Equipement", null)
            {
                ManufacturerReference = root.TryGetProperty("manufacturerReference", out var mr) && mr.ValueKind != JsonValueKind.Null
                    ? mr.GetString() : null,
            },
            _ => null
        };

        if (element is not null && entity.Placement is not null)
            element.Placement = new Transform3D(new Point3D(entity.Placement.X, entity.Placement.Y, entity.Placement.Z), 0);

        return element;
    }

    public static Room FromRoomEntity(RoomEntity entity, Level level)
    {
        var ring = entity.Boundary.ExteriorRing.Coordinates;
        // ToPolygon ferme l'anneau en dupliquant le premier point en fin de liste : on l'enleve pour
        // retrouver exactement le contour d'origine (docs ToPolygon).
        int count = ring.Length > 1 && ring[0].Equals2D(ring[^1]) ? ring.Length - 1 : ring.Length;

        return new Room
        {
            Name = entity.Name,
            Level = level,
            Boundary = ring.Take(count).Select(c => new Point3D(c.X, c.Y, c.Z)).ToList(),
            AreaM2 = entity.AreaM2 ?? 0,
            VolumeM3 = entity.VolumeM3 ?? 0,
            HeatingLoadW = entity.HeatingLoadW ?? 0,
            CoolingLoadW = entity.CoolingLoadW ?? 0,
        };
    }

    private static MepDuct FromDuct(BimElementEntity entity, JsonElement root)
    {
        var shape = Enum.Parse<DuctShape>(root.GetProperty("shape").GetString()!);
        var duct = new MepDuct(entity.Name ?? "Gaine", null, shape, root.GetProperty("lengthM").GetDouble());
        if (shape == DuctShape.Rectangular)
            duct.ResizeRectangular(root.GetProperty("widthM").GetDouble(), root.GetProperty("heightM").GetDouble());
        else
            duct.ResizeCircular(root.GetProperty("diameterM").GetDouble());
        duct.Material = root.GetProperty("material").GetString() ?? duct.Material;
        duct.InsulationThicknessM = root.GetProperty("insulationThicknessM").GetDouble();
        return duct;
    }

    private static MepPipe FromPipe(BimElementEntity entity, JsonElement root)
    {
        var systemType = Enum.Parse<SystemClassification>(root.GetProperty("systemType").GetString()!);
        var pipe = new MepPipe(entity.Name ?? "Tuyauterie", null, systemType, root.GetProperty("lengthM").GetDouble())
        {
            DiameterNominalM = root.GetProperty("diameterNominalM").GetDouble(),
            Material = root.GetProperty("material").GetString() ?? "PVC",
            SlopePercent = root.GetProperty("slopePercent").GetDouble(),
        };
        return pipe;
    }

    private static CableTray FromCableTray(BimElementEntity entity, JsonElement root) => new(entity.Name ?? "CdC", null)
    {
        WidthM = root.GetProperty("widthM").GetDouble(),
        HeightM = root.GetProperty("heightM").GetDouble(),
        LengthM = root.GetProperty("lengthM").GetDouble(),
        TrayType = root.GetProperty("trayType").GetString() ?? "Perfore",
    };

    private static Point ToPoint(Point3D p) => Geometry.CreatePoint(new CoordinateZ(p.X, p.Y, p.Z));

    private static Polygon ToPolygon(IReadOnlyList<Point3D> boundary)
    {
        var coordinates = boundary.Select(p => new CoordinateZ(p.X, p.Y, p.Z)).ToList();
        if (!coordinates[0].Equals2D(coordinates[^1]))
            coordinates.Add(coordinates[0]); // IfcPolyline/anneau : fermeture explicite requise par NetTopologySuite

        return Geometry.CreatePolygon(coordinates.ToArray());
    }
}
