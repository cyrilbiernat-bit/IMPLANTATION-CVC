using BimMep.Core.Bim;
using BimMep.Core.Geometry;
using BimMep.Core.Mep;

namespace BimMep.Core.Ifc;

/// <summary>
/// Exporte un <see cref="Project"/> (Core.Bim/Core.Mep) vers un fichier IFC4 (STEP), en suivant le
/// mapping documente dans docs/bim-mep-platform/06-schema-ifc.md §6.2. Cible la hierarchie spatiale
/// (Project/Site/Building/Storeys/Spaces) et les categories MEP couvertes par Core.Mep
/// (IfcDuctSegment, IfcPipeSegment, IfcCableCarrierSegment, IfcUnitaryEquipment), avec le GUID IFC
/// natif de chaque BimElement (docs §5.1, §6.4 — jamais regenere).
///
/// Simplifications assumees (voir docs/bim-mep-platform/13-modules-critiques.md) :
/// - placements en coordonnees absolues (PlacementRelTo = $ pour chaque IfcLocalPlacement) plutot
///   qu'une hierarchie de placements relatifs a la structure spatiale ;
/// - geometrie derivee des memes conventions simplifiees que Core.Geometry (direction horizontale
///   uniquement, cf. Transform3D — pas d'inclinaison de troncon) ;
/// - pas de Pset standard buildingSmart (Pset_DuctSegmentTypeCommon, etc.), un seul Pset propriétaire
///   minimal par element MEP (dimensions) pour illustrer le mecanisme (docs §6.2 derniere ligne).
/// </summary>
public sealed class IfcProjectExporter
{
    private readonly IfcStepWriter _writer = new();

    public static string Export(Project project) => new IfcProjectExporter().ExportInternal(project);

    private string ExportInternal(Project project)
    {
        var ownerHistory = CreateOwnerHistory();
        var context = CreateGeometricRepresentationContext();
        var unitAssignment = CreateUnitAssignment();

        var projectRef = _writer.Write("IFCPROJECT",
            IfcGuidGenerator.NewGuid(), ownerHistory, project.Name, null, null, null, null,
            new object?[] { context }, unitAssignment);

        var siteRef = CreateSpatialElement("IFCSITE", ownerHistory, "Site", extraAttributes:
            new object?[] { null, null, null, null, null }); // RefLatitude..SiteAddress

        var buildingRef = CreateSpatialElement("IFCBUILDING", ownerHistory, project.Name,
            extraAttributes: new object?[] { null, null, null }); // ElevationOfRefHeight..BuildingAddress

        _writer.Write("IFCRELAGGREGATES", NewGuid(), ownerHistory, null, null, projectRef, new object?[] { siteRef });
        _writer.Write("IFCRELAGGREGATES", NewGuid(), ownerHistory, null, null, siteRef, new object?[] { buildingRef });

        var levels = project.Levels.Count > 0
            ? project.Levels
            : new List<Level> { new() { Name = "Niveau 0", ElevationMeters = 0, HeightMeters = 3.0 } };

        var storeyByLevel = new Dictionary<Guid, IfcRef>();
        var storeyRefs = new List<IfcRef>();
        foreach (var level in levels)
        {
            var storeyRef = CreateBuildingStorey(ownerHistory, level);
            storeyByLevel[level.Id] = storeyRef;
            storeyRefs.Add(storeyRef);
        }
        _writer.Write("IFCRELAGGREGATES", NewGuid(), ownerHistory, null, null, buildingRef, storeyRefs.Cast<object?>());

        var defaultStorey = storeyRefs[0];

        // Regroupe les elements par etage pour emettre un seul IfcRelContainedInSpatialStructure par etage
        // (docs §6.2 — IfcRelAssignsToGroup/IfcRelContainedInSpatialStructure), plutot qu'une relation par element.
        var elementsByStorey = new Dictionary<IfcRef, List<IfcRef>>();
        void AddToStorey(IfcRef storey, IfcRef element)
        {
            if (!elementsByStorey.TryGetValue(storey, out var list))
                elementsByStorey[storey] = list = new List<IfcRef>();
            list.Add(element);
        }

        foreach (var room in project.Rooms)
        {
            var storey = room.Level is not null && storeyByLevel.TryGetValue(room.Level.Id, out var s) ? s : defaultStorey;
            var height = room.Level?.HeightMeters ?? 3.0;
            AddToStorey(storey, ExportRoom(ownerHistory, context, room, height));
        }

        foreach (var element in project.Elements)
        {
            var exported = ExportElement(ownerHistory, context, element);
            if (exported is null) continue; // categorie non prise en charge par cet exporteur (docs §17-21 perimetre)

            var storey = element.Level is not null && storeyByLevel.TryGetValue(element.Level.Id, out var s) ? s : defaultStorey;
            AddToStorey(storey, exported.Value);
        }

        foreach (var (storey, elements) in elementsByStorey)
        {
            _writer.Write("IFCRELCONTAINEDINSPATIALSTRUCTURE", NewGuid(), ownerHistory, null, null,
                elements.Cast<object?>(), storey);
        }

        return _writer.BuildDocument(
            fileDescription: "BimMepPlatform export",
            fileName: $"{project.Name}.ifc",
            schemaName: "IFC4");
    }

    // ------------------------------------------------------------------
    // Structure spatiale
    // ------------------------------------------------------------------

    private IfcRef CreateSpatialElement(string ifcType, IfcRef ownerHistory, string name, object?[] extraAttributes)
    {
        var placement = CreateLocalPlacement(new Point3D(0, 0, 0));
        object?[] common = { NewGuid(), ownerHistory, name, null, null, placement, null, null, new IfcEnum("ELEMENT") };
        return _writer.Write(ifcType, common.Concat(extraAttributes).ToArray());
    }

    private IfcRef CreateBuildingStorey(IfcRef ownerHistory, Level level)
    {
        var placement = CreateLocalPlacement(new Point3D(0, 0, level.ElevationMeters));
        return _writer.Write("IFCBUILDINGSTOREY",
            NewGuid(), ownerHistory, level.Name, null, null, placement, null, null,
            new IfcEnum("ELEMENT"), level.ElevationMeters);
    }

    private IfcRef ExportRoom(IfcRef ownerHistory, IfcRef context, Room room, double heightM)
    {
        var placement = CreateLocalPlacement(new Point3D(0, 0, 0));
        IfcRef? representation = null;

        if (room.Boundary.Count >= 3)
        {
            var points = room.Boundary.Select(p => CreateCartesianPoint(p.X, p.Y)).ToList();
            var polyline = _writer.Write("IFCPOLYLINE", points.Cast<object?>());
            var profile = _writer.Write("IFCARBITRARYCLOSEDPROFILEDEF", new IfcEnum("AREA"), null, polyline);
            var solidPosition = CreateAxis2Placement3D(new Point3D(0, 0, 0), null, null);
            var solid = _writer.Write("IFCEXTRUDEDAREASOLID", profile, solidPosition,
                CreateDirection(new Vector3D(0, 0, 1)), heightM);
            var shape = _writer.Write("IFCSHAPEREPRESENTATION", context, "Body", "SweptSolid", new object?[] { solid });
            representation = _writer.Write("IFCPRODUCTDEFINITIONSHAPE", null, null, new object?[] { shape });
        }

        return _writer.Write("IFCSPACE",
            IfcGuidGenerator.NewGuid(), ownerHistory, room.Name, null, null, placement, representation, null,
            new IfcEnum("ELEMENT"), null);
    }

    // ------------------------------------------------------------------
    // Elements MEP (docs §6.2)
    // ------------------------------------------------------------------

    private IfcRef? ExportElement(IfcRef ownerHistory, IfcRef context, BimElement element)
    {
        return element switch
        {
            MepDuct duct => ExportDuct(ownerHistory, context, duct),
            MepPipe pipe => ExportPipe(ownerHistory, context, pipe),
            CableTray tray => ExportCableTray(ownerHistory, context, tray),
            MepEquipment equipment => ExportEquipment(ownerHistory, context, equipment),
            _ => null
        };
    }

    private IfcRef ExportDuct(IfcRef ownerHistory, IfcRef context, MepDuct duct)
    {
        var profile = duct.Shape == DuctShape.Rectangular
            ? _writer.Write("IFCRECTANGLEPROFILEDEF", new IfcEnum("AREA"), null, null, duct.WidthM, duct.HeightM)
            : _writer.Write("IFCCIRCLEPROFILEDEF", new IfcEnum("AREA"), null, null, duct.DiameterM / 2.0);

        var representation = CreateLinearRepresentation(context, profile, duct.LengthM);
        var placement = CreateDirectedLocalPlacement(duct.Placement);

        var ifcElement = _writer.Write("IFCDUCTSEGMENT",
            duct.IfcGuid, ownerHistory, duct.Name, null, null, placement, representation, null, null);

        WriteDimensionPset(ownerHistory, ifcElement, "BimMep_DuctDimensions", ("LengthM", duct.LengthM),
            duct.Shape == DuctShape.Rectangular ? ("WidthM", duct.WidthM) : ("DiameterM", duct.DiameterM));

        return ifcElement;
    }

    private IfcRef ExportPipe(IfcRef ownerHistory, IfcRef context, MepPipe pipe)
    {
        var profile = _writer.Write("IFCCIRCLEPROFILEDEF", new IfcEnum("AREA"), null, null, pipe.DiameterNominalM / 2.0);
        var representation = CreateLinearRepresentation(context, profile, pipe.LengthM);
        var placement = CreateDirectedLocalPlacement(pipe.Placement);

        var ifcElement = _writer.Write("IFCPIPESEGMENT",
            pipe.IfcGuid, ownerHistory, pipe.Name, null, null, placement, representation, null, null);

        WriteDimensionPset(ownerHistory, ifcElement, "BimMep_PipeDimensions",
            ("LengthM", pipe.LengthM), ("DiameterNominalM", pipe.DiameterNominalM));

        return ifcElement;
    }

    private IfcRef ExportCableTray(IfcRef ownerHistory, IfcRef context, CableTray tray)
    {
        var profile = _writer.Write("IFCRECTANGLEPROFILEDEF", new IfcEnum("AREA"), null, null, tray.WidthM, tray.HeightM);
        var representation = CreateLinearRepresentation(context, profile, tray.LengthM);
        var placement = CreateDirectedLocalPlacement(tray.Placement);

        var ifcElement = _writer.Write("IFCCABLECARRIERSEGMENT",
            tray.IfcGuid, ownerHistory, tray.Name, null, null, placement, representation, null, null);

        WriteDimensionPset(ownerHistory, ifcElement, "BimMep_CableTrayDimensions",
            ("LengthM", tray.LengthM), ("WidthM", tray.WidthM), ("HeightM", tray.HeightM));

        return ifcElement;
    }

    private IfcRef ExportEquipment(IfcRef ownerHistory, IfcRef context, MepEquipment equipment)
    {
        // Geometrie de substitution (docs §12 — la geometrie reelle proviendrait de la famille BIM
        // fabricant, hors perimetre de cet exemple) : un cube de 0.6 m de cote centre sur le placement.
        const double placeholderSizeM = 0.6;
        var profile = _writer.Write("IFCRECTANGLEPROFILEDEF", new IfcEnum("AREA"), null, null, placeholderSizeM, placeholderSizeM);
        var representation = CreateLinearRepresentation(context, profile, placeholderSizeM);
        var placement = CreateDirectedLocalPlacement(equipment.Placement);

        var ifcElement = _writer.Write("IFCUNITARYEQUIPMENT",
            equipment.IfcGuid, ownerHistory, equipment.Name, null, null, placement, representation, null, null);

        if (equipment.ManufacturerReference is { } manufacturerRef)
        {
            WriteDimensionPset(ownerHistory, ifcElement, "BimMep_EquipmentInfo",
                ("ManufacturerReference", manufacturerRef));
        }

        return ifcElement;
    }

    // ------------------------------------------------------------------
    // Geometrie / placement
    // ------------------------------------------------------------------

    private IfcRef CreateLinearRepresentation(IfcRef context, IfcRef profile, double lengthM)
    {
        var solidPosition = CreateAxis2Placement3D(new Point3D(0, 0, 0), null, null);
        var solid = _writer.Write("IFCEXTRUDEDAREASOLID", profile, solidPosition,
            CreateDirection(new Vector3D(0, 0, 1)), lengthM);
        var shape = _writer.Write("IFCSHAPEREPRESENTATION", context, "Body", "SweptSolid", new object?[] { solid });
        return _writer.Write("IFCPRODUCTDEFINITIONSHAPE", null, null, new object?[] { shape });
    }

    /// <summary>
    /// Place l'element avec l'axe local Z de son repere aligne sur sa direction de pose (docs Core.Ifc,
    /// convention "extrusion le long de l'axe local Z" — cf. commentaire de classe). Le placement
    /// simplifie de Core.Geometry (angle de lacet uniquement) ne couvre que des troncons horizontaux.
    /// </summary>
    private IfcRef CreateDirectedLocalPlacement(Transform3D placement)
    {
        var direction = new Vector3D(Math.Cos(placement.YawRadians), Math.Sin(placement.YawRadians), 0);
        return CreateLocalPlacement(placement.Origin, direction, new Vector3D(0, 0, 1));
    }

    private IfcRef CreateLocalPlacement(Point3D location, Vector3D? axis = null, Vector3D? refDirection = null)
    {
        var axisPlacement = CreateAxis2Placement3D(location, axis, refDirection);
        return _writer.Write("IFCLOCALPLACEMENT", null, axisPlacement);
    }

    private IfcRef CreateAxis2Placement3D(Point3D location, Vector3D? axis, Vector3D? refDirection)
    {
        var locationRef = CreateCartesianPoint(location.X, location.Y, location.Z);
        IfcRef? axisRef = axis is { } a ? CreateDirection(a) : null;
        IfcRef? refDirRef = refDirection is { } r ? CreateDirection(r) : null;
        return _writer.Write("IFCAXIS2PLACEMENT3D", locationRef, axisRef, refDirRef);
    }

    /// <summary>
    /// IfcCartesianPoint ne porte qu'un seul attribut explicite (Coordinates : LIST OF IfcLengthMeasure) :
    /// il doit donc se serialiser avec une liste imbriquee, ex. IFCCARTESIANPOINT((0.,0.,0.)). Passer un
    /// <c>object?[]</c> litteral comme unique argument de <see cref="IfcStepWriter.Write"/> serait
    /// interprete par C# comme le tableau params lui-meme (passage direct, pas d'imbrication) — d'ou
    /// l'usage d'un <see cref="List{T}"/>, dont le type ne correspond pas exactement a <c>object?[]</c>
    /// et force donc le bon comportement d'encapsulation. Erreur reelle rencontree lors de la validation
    /// IfcOpenShell (docs §13-modules-critiques.md) : sans ce correctif, le point degenerait en trois
    /// attributs plats et le moteur geometrique OCCT echouait avec "Unexpected topology".
    /// </summary>
    private IfcRef CreateCartesianPoint(params double[] coordinates) =>
        _writer.Write("IFCCARTESIANPOINT", new List<object?>(coordinates.Cast<object?>()));

    private IfcRef CreateDirection(Vector3D v) =>
        _writer.Write("IFCDIRECTION", new List<object?> { v.X, v.Y, v.Z });

    // ------------------------------------------------------------------
    // Contexte, unites, historique
    // ------------------------------------------------------------------

    private IfcRef CreateGeometricRepresentationContext()
    {
        var worldOrigin = CreateAxis2Placement3D(new Point3D(0, 0, 0), null, null);
        return _writer.Write("IFCGEOMETRICREPRESENTATIONCONTEXT", null, "Model", 3, 1e-5, worldOrigin, null);
    }

    private IfcRef CreateUnitAssignment()
    {
        var length = _writer.Write("IFCSIUNIT", IfcDerived.Instance, new IfcEnum("LENGTHUNIT"), null, new IfcEnum("METRE"));
        var area = _writer.Write("IFCSIUNIT", IfcDerived.Instance, new IfcEnum("AREAUNIT"), null, new IfcEnum("SQUARE_METRE"));
        var volume = _writer.Write("IFCSIUNIT", IfcDerived.Instance, new IfcEnum("VOLUMEUNIT"), null, new IfcEnum("CUBIC_METRE"));
        var plane = _writer.Write("IFCSIUNIT", IfcDerived.Instance, new IfcEnum("PLANEANGLEUNIT"), null, new IfcEnum("RADIAN"));
        return _writer.Write("IFCUNITASSIGNMENT", new List<object?> { length, area, volume, plane });
    }

    private IfcRef CreateOwnerHistory()
    {
        var organization = _writer.Write("IFCORGANIZATION", null, "BimMepPlatform", null, null, null);
        // WR1 de IfcPerson exige Identification, FamilyName ou GivenName renseigne.
        var person = _writer.Write("IFCPERSON", null, "Ingenieur", null, null, null, null, null, null);
        var personAndOrg = _writer.Write("IFCPERSONANDORGANIZATION", person, organization, null);
        var application = _writer.Write("IFCAPPLICATION", organization, "0.1", "BimMepPlatform Core.Ifc Exporter", "BimMepPlatform");
        long unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return _writer.Write("IFCOWNERHISTORY", personAndOrg, application, null, new IfcEnum("ADDED"), null, null, null, (int)unixTime);
    }

    private string NewGuid() => IfcGuidGenerator.NewGuid();

    private void WriteDimensionPset(IfcRef ownerHistory, IfcRef element, string psetName, params (string Name, object Value)[] properties)
    {
        var propertyRefs = properties.Select(p =>
        {
            object typedValue = p.Value switch
            {
                double d => new IfcTypedLiteral("IFCREAL", d),
                string s => new IfcTypedLiteral("IFCLABEL", s),
                _ => new IfcTypedLiteral("IFCLABEL", p.Value.ToString() ?? string.Empty)
            };
            return (object?)_writer.Write("IFCPROPERTYSINGLEVALUE", p.Name, null, typedValue, null);
        }).ToList();

        var pset = _writer.Write("IFCPROPERTYSET", NewGuid(), ownerHistory, psetName, null, propertyRefs);
        _writer.Write("IFCRELDEFINESBYPROPERTIES", NewGuid(), ownerHistory, null, null, new object?[] { element }, pset);
    }
}
