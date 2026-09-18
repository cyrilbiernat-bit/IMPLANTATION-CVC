using Bim.Cvc.Api.Projects;
using PdfSharp.Pdf.IO;

namespace Bim.Cvc.Api.Tests;

public class ProjectReportPdfWriterTests
{
    private static readonly ProjectDto Project = new(Guid.NewGuid(), "Réhabilitation CVC — Bâtiment A", DateTimeOffset.UtcNow);

    private static readonly ProjectMetresDto Metres = new(
        Project.Id,
        DrawingCount: 3,
        TotalDuctLengthMeters: 142.7,
        TotalInsulationAreaM2: 58.3,
        TotalWeightKg: 612.4,
        AccessoryCounts:
        [
            new AccessoryCountDto("Bouche", 12),
            new AccessoryCountDto("Diffuseur", 4),
            new AccessoryCountDto("Cta", 1),
        ]);

    private static NomenclatureRowDto MakeRow(int i) => new(
        Guid.NewGuid(),
        DrawingFileName: $"plan-{i % 3}.pdf",
        LayerName: "Réseau soufflage",
        Type: i % 2 == 0 ? "GaineRectangulaire" : "GaineCirculaire",
        WidthMm: i % 2 == 0 ? 400 : null,
        HeightMm: i % 2 == 0 ? 200 : null,
        DiameterMm: i % 2 == 0 ? null : 250,
        LengthMeters: 3.2 + i,
        DebitM3h: 850,
        VitesseMs: 5.1,
        PressionPa: 120,
        WeightKg: 18.4,
        InsulationAreaM2: 2.1);

    [Fact]
    public void Write_produces_a_valid_single_page_pdf_for_a_small_project()
    {
        var rows = Enumerable.Range(0, 5).Select(MakeRow).ToList();

        var bytes = ProjectReportPdfWriter.Write(Project, Metres, rows);

        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);

        using var ms = new MemoryStream(bytes);
        using var document = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.Equal(1, document.PageCount);
    }

    [Fact]
    public void Write_paginates_across_multiple_pages_for_a_long_nomenclature()
    {
        var rows = Enumerable.Range(0, 120).Select(MakeRow).ToList();

        var bytes = ProjectReportPdfWriter.Write(Project, Metres, rows);

        using var ms = new MemoryStream(bytes);
        using var document = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.True(document.PageCount > 1, "120 lignes de nomenclature doivent déborder sur plusieurs pages.");
    }

    [Fact]
    public void Write_handles_a_project_with_no_cvc_objects_at_all()
    {
        var emptyMetres = Metres with { TotalDuctLengthMeters = 0, TotalInsulationAreaM2 = 0, TotalWeightKg = 0, AccessoryCounts = [] };

        var bytes = ProjectReportPdfWriter.Write(Project, emptyMetres, []);

        using var ms = new MemoryStream(bytes);
        using var document = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.Equal(1, document.PageCount);
    }
}
