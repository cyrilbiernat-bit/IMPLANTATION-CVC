namespace Bim.Cvc.Domain;

public enum CvcObjectType
{
    GaineRectangulaire,
    GaineCirculaire,
    Coude,
    Te,
    Reduction,
    Bouche,
    Diffuseur,
    Extracteur,
    Cta,
}

/// <summary>
/// Un objet CVC dessiné sur un plan (module 3). Les gaines portent
/// <see cref="Start"/>/<see cref="End"/> ; les accessoires, terminaux et
/// équipements portent <see cref="Position"/>. Les deux familles partagent
/// une seule classe plutôt qu'une hiérarchie : plus simple à persister et à
/// sérialiser pour ce que le MVP a besoin d'en faire.
/// </summary>
public sealed class CvcObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid DrawingId { get; init; }
    public required CvcObjectType Type { get; init; }

    /// <summary>Calque propriétaire (module 3). Réassigné automatiquement si son calque est supprimé.</summary>
    public required Guid LayerId { get; set; }

    // Gaines — segment entre deux points, en unités de dessin (mêmes que
    // la calibration : pixels PDF ou unités DXF/DWG selon le plan).
    public Point2D? Start { get; init; }
    public Point2D? End { get; init; }
    public double? WidthMm { get; init; }
    public double? HeightMm { get; init; }
    public double? DiameterMm { get; init; }

    // Accessoires / terminaux / équipements — un point.
    public Point2D? Position { get; init; }
    public double RotationRad { get; init; }

    // Module 4 — grandeurs aérauliques. Non calculées par le MVP (hors
    // périmètre : calcul aéraulique/hydraulique) ; le champ existe pour
    // être renseigné manuellement ou par un futur module de calcul/IA.
    public double? DebitM3h { get; init; }
    public double? VitesseMs { get; init; }
    public double? PressionPa { get; init; }

    /// <summary>Autres objets touchant celui-ci à un point commun (accrochage) — module 3 "connexion intelligente".</summary>
    public List<Guid> ConnectedObjectIds { get; } = [];

    public static bool IsDuct(CvcObjectType type) => type is CvcObjectType.GaineRectangulaire or CvcObjectType.GaineCirculaire;

    /// <summary>Points de connexion de cet objet, pour la détection d'accrochage.</summary>
    public IEnumerable<Point2D> ConnectionPoints()
    {
        if (Start is not null) yield return Start;
        if (End is not null) yield return End;
        if (Position is not null) yield return Position;
    }
}
