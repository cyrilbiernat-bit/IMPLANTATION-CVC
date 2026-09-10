namespace Bim.Cvc.Domain;

public sealed record CalibrationPoint(double X, double Y);

/// <summary>
/// Échelle réelle d'un plan, déduite de deux points cliqués par
/// l'utilisateur sur une cote connue (ex. un mur de 10 m). Les points sont
/// exprimés dans l'espace PDF de la page (indépendant du zoom/de la
/// rotation d'affichage), pour rester valables quel que soit le niveau de
/// zoom au moment de la calibration.
/// </summary>
public sealed record Calibration(
    int PageNumber,
    CalibrationPoint PointA,
    CalibrationPoint PointB,
    double RealDistanceMeters,
    double MetersPerPixel)
{
    public static Calibration Create(int pageNumber, CalibrationPoint pointA, CalibrationPoint pointB, double realDistanceMeters)
    {
        if (realDistanceMeters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(realDistanceMeters), "La distance réelle doit être positive.");
        }

        var pixelDistance = Math.Sqrt(Math.Pow(pointB.X - pointA.X, 2) + Math.Pow(pointB.Y - pointA.Y, 2));
        if (pixelDistance < 1e-6)
        {
            throw new ArgumentException("Les deux points de calibration sont confondus.");
        }

        return new Calibration(pageNumber, pointA, pointB, realDistanceMeters, realDistanceMeters / pixelDistance);
    }
}
