using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

/// <summary>
/// Module 3 — pose des gaines et des accessoires/terminaux/équipements sur
/// un plan déjà importé, avec accrochage automatique aux objets voisins
/// ("connexion intelligente") et respect du verrouillage des calques.
/// </summary>
public sealed class CvcObjectService(IDrawingRepository drawings, ICvcObjectRepository objects, LayerService layerService)
{
    /// <summary>Deux points de dessin à moins de 15 cm réels l'un de l'autre sont considérés connectés.</summary>
    private const double ConnectionToleranceMeters = 0.15;

    // Hypothèses de calcul du poids (module 4) — tôle acier galvanisé,
    // épaisseur usuelle pour une gaine basse pression. Une vraie sélection
    // par abaque (norme/DTU) est hors périmètre du MVP ; ce calcul reste
    // une estimation géométrique, pas un dimensionnement.
    private const double SheetThicknessM = 0.0006;
    private const double SteelDensityKgPerM3 = 7850;

    public CvcObject AddDuct(
        Guid drawingId,
        Guid layerId,
        CvcObjectType type,
        Point2D start,
        Point2D end,
        double? widthMm,
        double? heightMm,
        double? diameterMm,
        double? debitM3h = null,
        double? vitesseMs = null,
        double? pressionPa = null)
    {
        if (!CvcObject.IsDuct(type))
        {
            throw new InvalidCvcObjectException($"{type} n'est pas un type de gaine.");
        }

        var drawing = drawings.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);
        var layer = layerService.RequireUnlockedLayer(drawingId, layerId);

        if (Distance(start, end) < 1e-6)
        {
            throw new InvalidCvcObjectException("Les deux extrémités de la gaine sont confondues.");
        }

        if (type == CvcObjectType.GaineRectangulaire)
        {
            if (widthMm is not > 0 || heightMm is not > 0)
            {
                throw new InvalidCvcObjectException("Largeur et hauteur doivent être positives pour une gaine rectangulaire.");
            }
        }
        else if (diameterMm is not > 0)
        {
            throw new InvalidCvcObjectException("Le diamètre doit être positif pour une gaine circulaire.");
        }

        var duct = new CvcObject
        {
            DrawingId = drawingId,
            LayerId = layer.Id,
            Type = type,
            Start = start,
            End = end,
            WidthMm = type == CvcObjectType.GaineRectangulaire ? widthMm : null,
            HeightMm = type == CvcObjectType.GaineRectangulaire ? heightMm : null,
            DiameterMm = type == CvcObjectType.GaineCirculaire ? diameterMm : null,
            DebitM3h = debitM3h,
            VitesseMs = vitesseMs,
            PressionPa = pressionPa,
        };

        Connect(duct, drawing, objects.GetByDrawing(drawingId));
        objects.Add(duct);
        return duct;
    }

    public CvcObject AddPointObject(
        Guid drawingId,
        Guid layerId,
        CvcObjectType type,
        Point2D position,
        double rotationRad,
        double? debitM3h = null,
        double? vitesseMs = null,
        double? pressionPa = null)
    {
        if (CvcObject.IsDuct(type))
        {
            throw new InvalidCvcObjectException($"{type} se pose avec deux points (gaine), pas un point unique.");
        }

        var drawing = drawings.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);
        var layer = layerService.RequireUnlockedLayer(drawingId, layerId);
        var existingObjects = objects.GetByDrawing(drawingId);

        // Un raccord (coude/té/réduction) n'a pas de section propre saisie par
        // l'utilisateur — il reprend celle de la gaine à laquelle il se
        // raccorde (accrochage), pour ne pas retomber sur une section par
        // défaut arbitraire en 3D et dans les métrés.
        var (widthMm, heightMm, diameterMm) = IsFitting(type)
            ? InheritCrossSection(position, drawing, existingObjects)
            : (null, null, null);

        var obj = new CvcObject
        {
            DrawingId = drawingId,
            LayerId = layer.Id,
            Type = type,
            Position = position,
            RotationRad = rotationRad,
            WidthMm = widthMm,
            HeightMm = heightMm,
            DiameterMm = diameterMm,
            DebitM3h = debitM3h,
            VitesseMs = vitesseMs,
            PressionPa = pressionPa,
        };

        Connect(obj, drawing, existingObjects);
        objects.Add(obj);
        return obj;
    }

    /// <summary>
    /// Trace automatiquement un réseau entre deux points : une gaine directe
    /// s'ils sont alignés, sinon deux tronçons de gaine reliés par un coude à
    /// l'équerre — plutôt que de poser chaque tronçon à la main.
    /// </summary>
    public IReadOnlyList<CvcObject> AutoRoute(
        Guid drawingId,
        Guid layerId,
        CvcObjectType ductType,
        Point2D start,
        Point2D end,
        double? widthMm,
        double? heightMm,
        double? diameterMm,
        double? debitM3h = null,
        double? vitesseMs = null,
        double? pressionPa = null)
    {
        if (!CvcObject.IsDuct(ductType))
        {
            throw new InvalidCvcObjectException($"{ductType} n'est pas un type de gaine.");
        }
        if (Distance(start, end) < 1e-6)
        {
            throw new InvalidCvcObjectException("Le point de départ et le point d'arrivée de l'autorouting sont confondus.");
        }

        // Valide tout en amont : AddDuct/AddPointObject ne doivent plus
        // pouvoir échouer une fois la création commencée, pour ne jamais
        // laisser un tronçon orphelin si une étape suivante échouait.
        _ = drawings.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);
        layerService.RequireUnlockedLayer(drawingId, layerId);
        if (ductType == CvcObjectType.GaineRectangulaire)
        {
            if (widthMm is not > 0 || heightMm is not > 0)
            {
                throw new InvalidCvcObjectException("Largeur et hauteur doivent être positives pour une gaine rectangulaire.");
            }
        }
        else if (diameterMm is not > 0)
        {
            throw new InvalidCvcObjectException("Le diamètre doit être positif pour une gaine circulaire.");
        }

        bool sameX = Math.Abs(start.X - end.X) < 1e-6;
        bool sameY = Math.Abs(start.Y - end.Y) < 1e-6;
        if (sameX || sameY)
        {
            return [AddDuct(drawingId, layerId, ductType, start, end, widthMm, heightMm, diameterMm, debitM3h, vitesseMs, pressionPa)];
        }

        // Des deux coins d'équerre possibles, un seul correspond au sens de
        // cintrage que sait dessiner la vue 3D (Scene3DView.tsx ne modélise
        // qu'une seule orientation de coude) — on le choisit systématiquement
        // plutôt que d'ajouter une variante miroir du raccord.
        var dx = Math.Sign(end.X - start.X);
        var dy = Math.Sign(end.Y - start.Y);
        var verticalFirst = dx == dy;
        var corner = verticalFirst ? new Point2D(start.X, end.Y) : new Point2D(end.X, start.Y);
        var elbowRotationRad = verticalFirst
            ? (dy < 0 ? -Math.PI / 2 : Math.PI / 2)
            : (dx < 0 ? 0 : Math.PI);

        var leg1 = AddDuct(drawingId, layerId, ductType, start, corner, widthMm, heightMm, diameterMm, debitM3h, vitesseMs, pressionPa);
        var elbow = AddPointObject(drawingId, layerId, CvcObjectType.Coude, corner, elbowRotationRad);
        var leg2 = AddDuct(drawingId, layerId, ductType, corner, end, widthMm, heightMm, diameterMm, debitM3h, vitesseMs, pressionPa);
        return [leg1, elbow, leg2];
    }

    public IReadOnlyList<CvcObject> GetByDrawing(Guid drawingId)
    {
        _ = drawings.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);
        return objects.GetByDrawing(drawingId);
    }

    /// <summary>Longueur réelle d'une gaine, si le plan a été calibré (module 2). Null sinon.</summary>
    public double? LengthMeters(CvcObject duct)
    {
        if (duct.Start is null || duct.End is null) return null;
        var drawing = drawings.Get(duct.DrawingId);
        if (drawing?.Calibration is null) return null;
        return Distance(duct.Start, duct.End) * drawing.Calibration.MetersPerPixel;
    }

    /// <summary>Surface développée à calorifuger (module 4/6) — périmètre × longueur réelle. Null si non calibré.</summary>
    public double? InsulationAreaM2(CvcObject duct)
    {
        var length = LengthMeters(duct);
        var perimeter = PerimeterMeters(duct);
        return length is null || perimeter is null ? null : length * perimeter;
    }

    /// <summary>Poids estimé de la gaine (module 4/6), tôle galvanisée à épaisseur usuelle. Null si non calibré.</summary>
    public double? WeightKg(CvcObject duct)
    {
        var area = InsulationAreaM2(duct);
        return area is null ? null : area * SheetThicknessM * SteelDensityKgPerM3;
    }

    private static double? PerimeterMeters(CvcObject duct) => duct.Type switch
    {
        CvcObjectType.GaineRectangulaire when duct.WidthMm is { } w && duct.HeightMm is { } h => 2 * (w + h) / 1000,
        CvcObjectType.GaineCirculaire when duct.DiameterMm is { } d => Math.PI * d / 1000,
        _ => null,
    };

    public void Remove(Guid drawingId, Guid objectId)
    {
        _ = drawings.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);
        var existing = objects.Get(objectId);
        if (existing is null || existing.DrawingId != drawingId)
        {
            throw new InvalidCvcObjectException("Objet introuvable sur ce plan.");
        }
        if (layerService.IsLocked(existing.LayerId))
        {
            throw new InvalidCvcObjectException("Cet objet est sur un calque verrouillé.");
        }

        objects.Remove(objectId);
        foreach (var other in objects.GetByDrawing(drawingId))
        {
            other.ConnectedObjectIds.Remove(objectId);
        }
    }

    private static void Connect(CvcObject newObject, Drawing drawing, IReadOnlyList<CvcObject> existingObjects)
    {
        if (drawing.Calibration is null) return; // pas d'échelle fiable -> pas de détection automatique.

        var toleranceUnits = ConnectionToleranceMeters / drawing.Calibration.MetersPerPixel;

        foreach (var other in existingObjects)
        {
            var connected = newObject.ConnectionPoints()
                .SelectMany(_ => other.ConnectionPoints(), (a, b) => Distance(a, b))
                .Any(d => d <= toleranceUnits);

            if (!connected) continue;

            if (!newObject.ConnectedObjectIds.Contains(other.Id)) newObject.ConnectedObjectIds.Add(other.Id);
            if (!other.ConnectedObjectIds.Contains(newObject.Id)) other.ConnectedObjectIds.Add(newObject.Id);
        }
    }

    private static double Distance(Point2D a, Point2D b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    private static bool IsFitting(CvcObjectType type) =>
        type is CvcObjectType.Coude or CvcObjectType.Te or CvcObjectType.Reduction;

    private static (double? WidthMm, double? HeightMm, double? DiameterMm) InheritCrossSection(
        Point2D position, Drawing drawing, IReadOnlyList<CvcObject> existingObjects)
    {
        if (drawing.Calibration is null) return (null, null, null);

        var toleranceUnits = ConnectionToleranceMeters / drawing.Calibration.MetersPerPixel;
        var connectedDuct = existingObjects.FirstOrDefault(o =>
            CvcObject.IsDuct(o.Type) &&
            (o.WidthMm is not null || o.DiameterMm is not null) &&
            o.ConnectionPoints().Any(p => Distance(p, position) <= toleranceUnits));

        return connectedDuct is null ? (null, null, null) : (connectedDuct.WidthMm, connectedDuct.HeightMm, connectedDuct.DiameterMm);
    }
}
