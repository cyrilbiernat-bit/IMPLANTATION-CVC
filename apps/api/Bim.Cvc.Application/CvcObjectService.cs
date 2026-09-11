using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

/// <summary>
/// Module 3 — pose des gaines et des accessoires/terminaux/équipements sur
/// un plan déjà importé, avec accrochage automatique aux objets voisins
/// ("connexion intelligente").
/// </summary>
public sealed class CvcObjectService(IDrawingRepository drawings, ICvcObjectRepository objects)
{
    /// <summary>Deux points de dessin à moins de 15 cm réels l'un de l'autre sont considérés connectés.</summary>
    private const double ConnectionToleranceMeters = 0.15;

    public CvcObject AddDuct(
        Guid drawingId,
        CvcObjectType type,
        Point2D start,
        Point2D end,
        double? widthMm,
        double? heightMm,
        double? diameterMm)
    {
        if (!CvcObject.IsDuct(type))
        {
            throw new InvalidCvcObjectException($"{type} n'est pas un type de gaine.");
        }

        var drawing = drawings.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);

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
            Type = type,
            Start = start,
            End = end,
            WidthMm = type == CvcObjectType.GaineRectangulaire ? widthMm : null,
            HeightMm = type == CvcObjectType.GaineRectangulaire ? heightMm : null,
            DiameterMm = type == CvcObjectType.GaineCirculaire ? diameterMm : null,
        };

        Connect(duct, drawing, objects.GetByDrawing(drawingId));
        objects.Add(duct);
        return duct;
    }

    public CvcObject AddPointObject(Guid drawingId, CvcObjectType type, Point2D position, double rotationRad)
    {
        if (CvcObject.IsDuct(type))
        {
            throw new InvalidCvcObjectException($"{type} se pose avec deux points (gaine), pas un point unique.");
        }

        var drawing = drawings.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);

        var obj = new CvcObject
        {
            DrawingId = drawingId,
            Type = type,
            Position = position,
            RotationRad = rotationRad,
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

    public void Remove(Guid drawingId, Guid objectId)
    {
        _ = drawings.Get(drawingId) ?? throw new DrawingNotFoundException(drawingId);
        var existing = objects.Get(objectId);
        if (existing is null || existing.DrawingId != drawingId)
        {
            throw new InvalidCvcObjectException("Objet introuvable sur ce plan.");
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
