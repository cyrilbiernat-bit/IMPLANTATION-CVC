using Bim.Cvc.Domain;

namespace Bim.Cvc.Api.Drawings;

public sealed record DrawingDto(
    Guid Id,
    string FileName,
    string BlobUrl,
    int NbPages,
    DateTimeOffset UploadedAt,
    CalibrationDto? Calibration)
{
    public static DrawingDto From(Drawing drawing, HttpRequest request)
    {
        var blobUrl = $"{request.Scheme}://{request.Host}/api/v1/drawings/{drawing.Id}/file";
        var calibration = drawing.Calibration is null ? null : CalibrationDto.From(drawing.Calibration);
        return new DrawingDto(drawing.Id, drawing.FileName, blobUrl, drawing.NbPages, drawing.UploadedAt, calibration);
    }
}
