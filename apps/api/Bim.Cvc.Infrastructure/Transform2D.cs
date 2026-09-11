using Bim.Cvc.Domain;

namespace Bim.Cvc.Infrastructure;

/// <summary>
/// Translation + rotation + mise à l'échelle uniforme, utilisée pour
/// aplatir les références de bloc (INSERT) DXF/DWG dans l'espace du plan.
/// Une échelle non uniforme (X/Y différents) sur un bloc n'est pas gérée —
/// cas rare pour des symboles CVC, simplification assumée pour le MVP.
/// </summary>
internal readonly struct Transform2D(double tx, double ty, double rotationRad, double scale)
{
    public static readonly Transform2D Identity = new(0, 0, 0, 1);

    public double RotationRad { get; } = rotationRad;

    public double Scale { get; } = scale;

    public Point2D Apply(double x, double y)
    {
        var sx = x * Scale;
        var sy = y * Scale;
        var cos = Math.Cos(RotationRad);
        var sin = Math.Sin(RotationRad);
        return new Point2D(sx * cos - sy * sin + tx, sx * sin + sy * cos + ty);
    }

    /// <summary>Compose cette transformation avec celle, locale, d'un bloc inséré à l'intérieur.</summary>
    public Transform2D Combine(double insertTx, double insertTy, double insertRotationRad, double insertScale)
    {
        var origin = Apply(insertTx, insertTy);
        return new Transform2D(origin.X, origin.Y, RotationRad + insertRotationRad, Scale * insertScale);
    }
}
