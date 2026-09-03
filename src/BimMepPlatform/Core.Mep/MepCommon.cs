using BimMep.Core.Bim;
using BimMep.Core.Geometry;

namespace BimMep.Core.Mep;

public enum ConnectorType { DuctRound, DuctRectangular, Pipe, CableTray, Electrical }

/// <summary>
/// Classification de systeme portee par chaque connecteur (docs §5.6). Contraint le routage :
/// deux systemes incompatibles ne peuvent pas partager un meme chemin sans regle explicite.
/// </summary>
public enum SystemClassification
{
    SupplyAir, ExtractAir,
    ChwSupply, ChwReturn, HhwSupply, HhwReturn,
    DomesticColdWater, DomesticHotWater,
    WasteEu, WasteEv, RainwaterEp,
    PowerNormal, PowerBackup, Data
}

/// <summary>Port de connexion d'un element MEP (docs §5, mappe vers IfcDistributionPort — §6.2).</summary>
public sealed class MepConnector
{
    public Guid Id { get; } = Guid.NewGuid();
    public required Guid OwnerElementId { get; init; }
    public required ConnectorType Type { get; init; }
    public Point3D Position { get; set; }
    public Vector3D Direction { get; set; }
    public double SizePrimary { get; set; }     // diametre, ou largeur si rectangulaire
    public double SizeSecondary { get; set; }   // hauteur si rectangulaire, 0 sinon
    public SystemClassification System { get; init; }
    public MepConnector? ConnectedTo { get; private set; }

    /// <summary>
    /// Connecte deux connecteurs compatibles. Refuse la connexion entre systemes incompatibles
    /// (ex. tenter de relier un reseau EU a un reseau EP) — controle applique aussi par le routage
    /// (Core.Routing.RoutingConstraints.AllowedCrossings) mais verifie ici en dernier recours.
    /// </summary>
    public void ConnectTo(MepConnector other)
    {
        if (Type != other.Type)
            throw new InvalidOperationException($"Connecteurs de types incompatibles : {Type} vs {other.Type}.");
        ConnectedTo = other;
        other.ConnectedTo = this;
    }

    public bool IsConnected => ConnectedTo is not null;
}

public enum NetworkKind { Aeraulique, HydrauliqueChauffage, HydrauliqueFroid, EuEvEp, Cfo, Cfa }

public sealed record LossReport(double TotalPressureLossPa, double MaxVelocityMs, IReadOnlyList<string> Warnings);

/// <summary>
/// Regroupement logique d'elements MEP formant un reseau (docs §5.6, table mep_networks).
/// Porte la topologie du reseau et agrege les calculs de pertes (Core.Calculations en delegue ici
/// un exemple simplifie pour l'aeraulique — cf. docs §15.9 objectifs de performance sur le recalcul).
/// </summary>
public sealed class MepNetwork
{
    public Guid Id { get; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required NetworkKind Kind { get; init; }
    public double DesignFlowM3H { get; set; }

    public List<BimElement> Members { get; } = new();

    private const double MaxRecommendedVelocityMs = 6.0;

    /// <summary>
    /// Calcul aeraulique simplifie : somme des pertes de charge lineaires des troncons de gaine du
    /// reseau (docs §7.4 exemple "optimise le poids des gaines" s'appuie sur ce type de rapport pour
    /// comparer des variantes). Les coefficients (0.02 friction lineaire) sont illustratifs — la
    /// version de production utilise les abaques normalises (Core.Calculations, docs §15.1 EN 16798).
    /// </summary>
    public LossReport ComputeLosses()
    {
        double totalLossPa = 0.0;
        double maxVelocity = 0.0;
        var warnings = new List<string>();

        foreach (var duct in Members.OfType<MepDuct>())
        {
            double areaM2 = duct.CrossSectionAreaM2;
            if (areaM2 <= 0) continue;

            double flowM3S = DesignFlowM3H / 3600.0;
            double velocityMs = flowM3S / areaM2;
            maxVelocity = Math.Max(maxVelocity, velocityMs);

            const double frictionFactor = 0.02;
            double hydraulicDiameterM = duct.HydraulicDiameterM;
            double lossPa = frictionFactor * (duct.LengthM / Math.Max(hydraulicDiameterM, 1e-3))
                            * (1.2 * velocityMs * velocityMs / 2.0);
            totalLossPa += lossPa;

            if (velocityMs > MaxRecommendedVelocityMs)
            {
                warnings.Add($"Troncon '{duct.Name}' : vitesse {velocityMs:F1} m/s > seuil recommande " +
                             $"{MaxRecommendedVelocityMs:F1} m/s.");
            }
        }

        return new LossReport(totalLossPa, maxVelocity, warnings);
    }
}
