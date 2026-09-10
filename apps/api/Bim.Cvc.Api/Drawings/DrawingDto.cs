using Bim.Cvc.Domain;

namespace Bim.Cvc.Api.Drawings;

public sealed record DrawingDto(Guid Id, string FileName, string BlobUrl, int NbPages, DateTimeOffset UploadedAt)
{
    public static DrawingDto From(Drawing drawing, HttpRequest request)
    {
        var blobUrl = $"{request.Scheme}://{request.Host}/api/v1/drawings/{drawing.Id}/file";
        return new DrawingDto(drawing.Id, drawing.FileName, blobUrl, drawing.NbPages, drawing.UploadedAt);
    }
}
