using BimMep.Core.Bim;
using BimMep.Core.Geometry;

namespace BimMep.Core.Mep;

public sealed class MepPipe : BimElement
{
    public double DiameterNominalM { get; set; }
    public SystemClassification SystemType { get; }
    public string Material { get; set; } = "PVC";
    public double SlopePercent { get; set; }
    public double LengthM { get; set; }
    public List<MepConnector> Connectors { get; } = new();

    public MepPipe(string name, FamilyType? familyType, SystemClassification systemType, double lengthM)
        : base(name, familyType)
    {
        SystemType = systemType;
        LengthM = lengthM;

        bool isGravityNetwork = systemType is SystemClassification.WasteEu or SystemClassification.WasteEv or SystemClassification.RainwaterEp;
        if (isGravityNetwork)
            SlopePercent = 1.0; // pente minimale par defaut (docs F-ROUTE-03), ajustable par l'ingenieur
    }

    public MepConnector AddConnector(Point3D position, Vector3D direction)
    {
        var connector = new MepConnector
        {
            OwnerElementId = Id,
            Type = ConnectorType.Pipe,
            Position = position,
            Direction = direction,
            SizePrimary = DiameterNominalM,
            System = SystemType
        };
        Connectors.Add(connector);
        return connector;
    }

    public override string GetIfcType() => "IfcPipeSegment";

    public override ValidationResult Validate()
    {
        var result = ValidationResult.Ok();
        bool isGravityNetwork = SystemType is SystemClassification.WasteEu or SystemClassification.WasteEv or SystemClassification.RainwaterEp;
        if (isGravityNetwork && SlopePercent < 1.0)
        {
            result.Issues.Add(new ValidationIssue(
                "PENTE_INSUFFISANTE",
                $"Le troncon '{Name}' ({SystemType}) a une pente de {SlopePercent:F2}% ; " +
                "une pente minimale de 1% est generalement requise pour un ecoulement gravitaire.",
                ValidationSeverity.Warning));
        }
        return result;
    }
}

public sealed class Cable
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Reference { get; init; }
    public required double CrossSectionMm2 { get; init; }
    public SystemClassification System { get; init; } = SystemClassification.PowerNormal;
}

public sealed class CableTray : BimElement
{
    public double WidthM { get; set; }
    public double HeightM { get; set; }
    public string TrayType { get; set; } = "Perfore";
    public List<Cable> Cables { get; } = new();

    public CableTray(string name, FamilyType? familyType) : base(name, familyType) { }

    public double FillRatio(double totalCableCrossSectionMm2Capacity) =>
        Cables.Sum(c => c.CrossSectionMm2) / Math.Max(totalCableCrossSectionMm2Capacity, 1e-6);

    public override string GetIfcType() => "IfcCableCarrierSegment";
}

/// <summary>
/// Equipement MEP (CTA, pompe, chaudiere, ...). Reference optionnellement un produit du catalogue
/// fabricants (docs §12, Services.Catalog) — c'est cet objet que cree le copilote IA en reponse a
/// "Place une CTA de 20 000 m3/h" (docs §7.3).
/// </summary>
public sealed class MepEquipment : BimElement
{
    public string? ManufacturerReference { get; set; }
    public Dictionary<string, double> PerformanceCurve { get; } = new();
    public List<MepConnector> Connectors { get; } = new();

    public MepEquipment(string name, FamilyType? familyType) : base(name, familyType) { }

    public MepConnector AddConnector(ConnectorType type, Point3D position, Vector3D direction, SystemClassification system, double sizePrimary)
    {
        var connector = new MepConnector
        {
            OwnerElementId = Id,
            Type = type,
            Position = position,
            Direction = direction,
            SizePrimary = sizePrimary,
            System = system
        };
        Connectors.Add(connector);
        return connector;
    }

    public override string GetIfcType() => "IfcUnitaryEquipment";
}
