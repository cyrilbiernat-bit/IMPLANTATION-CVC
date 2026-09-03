using BimMep.Core.Bim;
using BimMep.Core.Geometry;

namespace BimMep.Core.Mep;

public enum DuctShape { Rectangular, Circular }

/// <summary>
/// Troncon de gaine (docs §5, mappe vers IfcDuctSegment/IfcDuctFitting selon geometrie — §6.2).
///
/// Illustre l'exemple de propagation parametrique du cahier des charges : modifier
/// Width/Height (ex. 800x400 -> 1000x500) puis appeler Recompute() regenere la section, recalcule
/// la surface hydraulique et notifie les raccords connectes via les connecteurs (docs §5.4).
/// </summary>
public sealed class MepDuct : BimElement
{
    public DuctShape Shape { get; private set; }
    public double WidthM { get; private set; }
    public double HeightM { get; private set; }
    public double DiameterM { get; private set; }
    public double LengthM { get; set; }
    public string Material { get; set; } = "Acier galvanise";
    public double InsulationThicknessM { get; set; }

    public List<MepConnector> Connectors { get; } = new();

    public MepDuct(string name, FamilyType? familyType, DuctShape shape, double lengthM)
        : base(name, familyType)
    {
        Shape = shape;
        LengthM = lengthM;
    }

    public double CrossSectionAreaM2 => Shape switch
    {
        DuctShape.Rectangular => WidthM * HeightM,
        DuctShape.Circular => Math.PI * DiameterM * DiameterM / 4.0,
        _ => 0.0
    };

    /// <summary>Diametre hydraulique — utilise pour les pertes de charge (docs Core.Calculations).</summary>
    public double HydraulicDiameterM => Shape switch
    {
        DuctShape.Rectangular => 2.0 * WidthM * HeightM / Math.Max(WidthM + HeightM, 1e-6),
        DuctShape.Circular => DiameterM,
        _ => 0.0
    };

    /// <summary>
    /// Redimensionne un troncon rectangulaire (ex. 800x400 -> 1000x500). Marque l'element dirty :
    /// c'est le RecomputeScheduler (Core.Bim) qui declenchera ensuite Recompute() dans le bon ordre
    /// avec la cascade sur les raccords et le reseau (docs §5.4, sequence diagram).
    /// </summary>
    public void ResizeRectangular(double newWidthM, double newHeightM)
    {
        if (Shape != DuctShape.Rectangular)
            throw new InvalidOperationException("ResizeRectangular ne s'applique qu'a une gaine rectangulaire.");
        WidthM = newWidthM;
        HeightM = newHeightM;
        SetParameter("Width", newWidthM);
        SetParameter("Height", newHeightM);
        MarkDirty();
    }

    public void ResizeCircular(double newDiameterM)
    {
        if (Shape != DuctShape.Circular)
            throw new InvalidOperationException("ResizeCircular ne s'applique qu'a une gaine circulaire.");
        DiameterM = newDiameterM;
        SetParameter("Diameter", newDiameterM);
        MarkDirty();
    }

    public MepConnector AddConnector(Point3D position, Vector3D direction, SystemClassification system)
    {
        var connector = new MepConnector
        {
            OwnerElementId = Id,
            Type = Shape == DuctShape.Rectangular ? ConnectorType.DuctRectangular : ConnectorType.DuctRound,
            Position = position,
            Direction = direction,
            System = system
        };
        SyncConnectorSize(connector);
        Connectors.Add(connector);
        return connector;
    }

    private void SyncConnectorSize(MepConnector connector)
    {
        if (Shape == DuctShape.Rectangular)
        {
            connector.SizePrimary = WidthM;
            connector.SizeSecondary = HeightM;
        }
        else
        {
            connector.SizePrimary = DiameterM;
            connector.SizeSecondary = 0;
        }
    }

    public override string GetIfcType() => "IfcDuctSegment";

    /// <summary>
    /// Regenere la geometrie derivee (section, connecteurs) a partir des parametres courants,
    /// puis signale un avertissement si le redimensionnement cree une discontinuite de section
    /// avec un connecteur voisin deja connecte (le raccord de transition devra etre insere —
    /// action laissee a l'ingenieur ou au copilote IA, jamais automatique et silencieuse).
    /// </summary>
    public override RecomputeOutcome Recompute()
    {
        var baseOutcome = base.Recompute();
        if (!baseOutcome.Changed) return baseOutcome;

        foreach (var connector in Connectors)
        {
            double previousPrimary = connector.SizePrimary;
            double previousSecondary = connector.SizeSecondary;
            SyncConnectorSize(connector);

            bool sizeChanged = Math.Abs(previousPrimary - connector.SizePrimary) > 1e-6
                                || Math.Abs(previousSecondary - connector.SizeSecondary) > 1e-6;

            if (sizeChanged && connector.IsConnected)
            {
                var neighbor = connector.ConnectedTo!;
                bool neighborMatches = Math.Abs(neighbor.SizePrimary - connector.SizePrimary) < 1e-6
                                        && Math.Abs(neighbor.SizeSecondary - connector.SizeSecondary) < 1e-6;
                if (!neighborMatches)
                {
                    return baseOutcome with
                    {
                        Warning = $"Discontinuite de section apres redimensionnement de '{Name}' : " +
                                  $"un raccord de transition est requis au connecteur {connector.Id}."
                    };
                }
            }
        }

        return baseOutcome;
    }
}
