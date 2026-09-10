using Bim.Cvc.Domain;

namespace Bim.Cvc.Api.Drawings;

public sealed record PointDto(double X, double Y)
{
    public CalibrationPoint ToDomain() => new(X, Y);
}

public sealed record CalibrationDto(int PageNumber, PointDto PointA, PointDto PointB, double RealDistanceMeters, double MetersPerPixel)
{
    public static CalibrationDto From(Calibration calibration) => new(
        calibration.PageNumber,
        new PointDto(calibration.PointA.X, calibration.PointA.Y),
        new PointDto(calibration.PointB.X, calibration.PointB.Y),
        calibration.RealDistanceMeters,
        calibration.MetersPerPixel);
}

public sealed record CalibrateDrawingRequest(int PageNumber, PointDto PointA, PointDto PointB, double RealDistanceMeters);
