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

        var obj = new CvcObject
        {
            DrawingId = drawingId,
            LayerId = layer.Id,
            Type = type,
            Position = position,
            RotationRad = rotationRad,
            DebitM3h = debitM3h,
            VitesseMs = vitesseMs,
            PressionPa = pressionPa,
        };

        Connect(obj, drawing, objects.GetByDrawing(drawingId));
        objects.Add(obj);
        return obj;
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
}
