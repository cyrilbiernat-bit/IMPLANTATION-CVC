using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Bim.Cvc.Api.Projects;

/// <summary>
/// Lot 2 — génère un rapport PDF de synthèse (métrés + nomenclature) pour un
/// projet, à partir des mêmes données que les endpoints JSON/CSV existants
/// (modules 5 et 6).
/// </summary>
public static class ProjectReportPdfWriter
{
    private const double MarginPt = 40;
    private static readonly XFont TitleFont = new("Liberation Sans", 20, XFontStyleEx.Bold);
    private static readonly XFont SubtitleFont = new("Liberation Sans", 13, XFontStyleEx.Regular);
    private static readonly XFont HeadingFont = new("Liberation Sans", 13, XFontStyleEx.Bold);
    private static readonly XFont TableHeaderFont = new("Liberation Sans", 8.5, XFontStyleEx.Bold);
    private static readonly XFont TableFont = new("Liberation Sans", 8.5, XFontStyleEx.Regular);
    private static readonly XFont SmallFont = new("Liberation Sans", 8, XFontStyleEx.Regular);
    private static readonly XBrush HeaderRowBrush = new XSolidBrush(XColor.FromArgb(230, 235, 241));
    private static readonly XPen TableBorderPen = new(XColor.FromArgb(200, 200, 200), 0.5);

    private static readonly Dictionary<string, string> TypeLabels = new()
    {
        ["GaineRectangulaire"] = "Gaine rectangulaire",
        ["GaineCirculaire"] = "Gaine circulaire",
        ["Coude"] = "Coude",
        ["Te"] = "Té",
        ["Reduction"] = "Réduction",
        ["Bouche"] = "Bouche",
        ["Diffuseur"] = "Diffuseur",
        ["Extracteur"] = "Extracteur",
        ["Cta"] = "CTA",
    };

    private static string TypeLabel(string type) => TypeLabels.GetValueOrDefault(type, type);

    public static byte[] Write(ProjectDto project, ProjectMetresDto metres, IReadOnlyList<NomenclatureRowDto> nomenclature)
    {
        var document = new PdfDocument();
        var page = new PageWriter(document);

        DrawHeader(page, project);
        DrawMetresSummary(page, metres);
        DrawNomenclature(page, nomenclature);

        using var ms = new MemoryStream();
        document.Save(ms, closeStream: false);
        return ms.ToArray();
    }

    private static void DrawHeader(PageWriter page, ProjectDto project)
    {
        page.Gfx.DrawString("Rapport de projet CVC", TitleFont, XBrushes.Black, new XPoint(MarginPt, page.Y + 20));
        page.Y += 30;
        page.Gfx.DrawString(project.Name, SubtitleFont, XBrushes.Black, new XPoint(MarginPt, page.Y + 15));
        page.Y += 20;
        page.Gfx.DrawString(
            $"Généré le {DateTimeOffset.Now:dd/MM/yyyy à HH:mm}",
            SmallFont,
            XBrushes.Gray,
            new XPoint(MarginPt, page.Y + 12));
        page.Y += 26;
    }

    private static void DrawMetresSummary(PageWriter page, ProjectMetresDto metres)
    {
        DrawSectionHeading(page, "Métrés (module 5)");

        var summaryRows = new[]
        {
            ("Nombre de plans", metres.DrawingCount.ToString()),
            ("Longueur totale de gaines", $"{metres.TotalDuctLengthMeters:0.##} m"),
            ("Surface totale à calorifuger", $"{metres.TotalInsulationAreaM2:0.##} m²"),
            ("Poids total du réseau", $"{metres.TotalWeightKg:0.##} kg"),
        };
        page.DrawTable(
            ["Indicateur", "Valeur"],
            [0.65, 0.35],
            summaryRows.Select(r => new[] { r.Item1, r.Item2 }).ToList());
        page.Y += 14;

        if (metres.AccessoryCounts.Count > 0)
        {
            page.DrawTable(
                ["Accessoires / terminaux", "Quantité"],
                [0.65, 0.35],
                metres.AccessoryCounts.Select(a => new[] { TypeLabel(a.Type), a.Count.ToString() }).ToList());
            page.Y += 14;
        }
    }

    private static void DrawNomenclature(PageWriter page, IReadOnlyList<NomenclatureRowDto> rows)
    {
        DrawSectionHeading(page, "Nomenclature détaillée (module 6)");

        if (rows.Count == 0)
        {
            page.Gfx.DrawString("Aucun objet CVC posé sur ce projet.", TableFont, XBrushes.Black, new XPoint(MarginPt, page.Y + 10));
            page.Y += 16;
            return;
        }

        var tableRows = rows.Select(r => new[]
        {
            r.DrawingFileName,
            r.LayerName,
            TypeLabel(r.Type),
            FormatDimensions(r),
            FormatNumber(r.LengthMeters, "m"),
            FormatNumber(r.DebitM3h, "m³/h"),
            FormatNumber(r.WeightKg, "kg"),
            FormatNumber(r.InsulationAreaM2, "m²"),
        }).ToList();

        page.DrawTable(
            ["Plan", "Calque", "Type", "Dimensions", "Longueur", "Débit", "Poids", "Surf. calo."],
            [0.14, 0.10, 0.17, 0.15, 0.10, 0.12, 0.11, 0.11],
            tableRows);
    }

    private static void DrawSectionHeading(PageWriter page, string text)
    {
        page.EnsureSpace(40);
        page.Gfx.DrawString(text, HeadingFont, XBrushes.Black, new XPoint(MarginPt, page.Y + 14));
        page.Y += 24;
    }

    // "Ø" (et non "⌀", absent de la police Liberation Sans embarquée — il s'affichait comme un
    // rectangle vide dans le PDF) est la notation usuelle du diamètre sur les plans imprimés.
    private static string FormatDimensions(NomenclatureRowDto row) => row.DiameterMm is { } diameter
        ? $"Ø{diameter:0.##} mm"
        : row.WidthMm is { } width && row.HeightMm is { } height
            ? $"{width:0.##}×{height:0.##} mm"
            : "—";

    private static string FormatNumber(double? value, string unit) =>
        value is null ? "—" : $"{value.Value.ToString("0.##")} {unit}";

    /// <summary>Gère la position d'écriture courante et le passage à la page suivante quand le contenu déborde.</summary>
    private sealed class PageWriter
    {
        private readonly PdfDocument _document;
        private int _pageNumber;
        private double _usableWidth;
        private double _bottomLimit;

        public PageWriter(PdfDocument document)
        {
            _document = document;
            NewPage();
        }

        public PdfPage Page { get; private set; } = null!;
        public XGraphics Gfx { get; private set; } = null!;
        public double Y { get; set; }

        public void EnsureSpace(double neededHeight)
        {
            if (Y + neededHeight > _bottomLimit)
            {
                NewPage();
            }
        }

        public void DrawTable(string[] headers, double[] columnWidthFractions, IReadOnlyList<string[]> rows)
        {
            const double rowHeight = 16;
            const double cellPaddingX = 3;
            var columnWidths = columnWidthFractions.Select(f => f * _usableWidth).ToArray();

            string Fit(string text, XFont font, double maxWidth)
            {
                if (Gfx.MeasureString(text, font).Width <= maxWidth) return text;
                var truncated = text;
                while (truncated.Length > 1 && Gfx.MeasureString(truncated + "…", font).Width > maxWidth)
                {
                    truncated = truncated[..^1];
                }
                return truncated.Length <= 1 ? truncated : truncated + "…";
            }

            void DrawHeaderRow()
            {
                EnsureSpace(rowHeight);
                Gfx.DrawRectangle(HeaderRowBrush, MarginPt, Y, _usableWidth, rowHeight);
                var x = MarginPt;
                for (var i = 0; i < headers.Length; i++)
                {
                    var maxWidth = columnWidths[i] - 2 * cellPaddingX;
                    Gfx.DrawString(Fit(headers[i], TableHeaderFont, maxWidth), TableHeaderFont, XBrushes.Black,
                        new XRect(x + cellPaddingX, Y, columnWidths[i] - cellPaddingX, rowHeight),
                        XStringFormats.CenterLeft);
                    x += columnWidths[i];
                }
                Gfx.DrawRectangle(TableBorderPen, MarginPt, Y, _usableWidth, rowHeight);
                Y += rowHeight;
            }

            DrawHeaderRow();
            foreach (var row in rows)
            {
                if (Y + rowHeight > _bottomLimit)
                {
                    NewPage();
                    DrawHeaderRow();
                }

                var x = MarginPt;
                for (var i = 0; i < row.Length; i++)
                {
                    var maxWidth = columnWidths[i] - 2 * cellPaddingX;
                    Gfx.DrawString(Fit(row[i], TableFont, maxWidth), TableFont, XBrushes.Black,
                        new XRect(x + cellPaddingX, Y, columnWidths[i] - cellPaddingX, rowHeight),
                        XStringFormats.CenterLeft);
                    x += columnWidths[i];
                }
                Gfx.DrawRectangle(TableBorderPen, MarginPt, Y, _usableWidth, rowHeight);
                Y += rowHeight;
            }
        }

        private void NewPage()
        {
            Page = _document.AddPage();
            Page.Size = PdfSharp.PageSize.A4;
            Gfx = XGraphics.FromPdfPage(Page);
            _usableWidth = Page.Width.Point - 2 * MarginPt;
            _bottomLimit = Page.Height.Point - MarginPt - 16;
            Y = MarginPt;
            _pageNumber++;

            Gfx.DrawString(
                $"Page {_pageNumber}",
                SmallFont,
                XBrushes.Gray,
                new XRect(MarginPt, Page.Height.Point - MarginPt, _usableWidth, 16),
                XStringFormats.CenterRight);
        }
    }
}
