namespace Bim.Cvc.Domain;

public sealed record Point2D(double X, double Y);

/// <summary>
/// Géométrie 2D extraite d'un plan vectoriel (DXF/DWG), dans l'unité de
/// dessin d'origine du fichier — comme le PDF, ce n'est qu'un fond de plan
/// de référence tant que le module 2 (calibration) n'a pas fixé l'échelle
/// réelle.
/// </summary>
public abstract record PlanEntity;

public sealed record PlanLine(Point2D Start, Point2D End) : PlanEntity;

public sealed record PlanPolyline(IReadOnlyList<Point2D> Points, bool Closed) : PlanEntity;

public sealed record PlanCircle(Point2D Center, double Radius) : PlanEntity;

public sealed record PlanArc(Point2D Center, double Radius, double StartAngleRad, double EndAngleRad) : PlanEntity;

public sealed record PlanText(Point2D Position, string Value, double Height) : PlanEntity;
