using System.Text;
using Bim.Cvc.Domain;
using Xunit;

namespace Bim.Cvc.Infrastructure.Tests;

public sealed class PdfPlanParserTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void Parse_counts_pages_and_returns_no_vector_entities(int pageCount)
    {
        using var pdf = BuildMinimalPdf(pageCount);

        var result = new PdfPlanParser().Parse(PlanFormat.Pdf, pdf);

        Assert.Equal(pageCount, result.PageCount);
        Assert.Null(result.Entities);
    }

    /// <summary>PDF minimal fait main (mêmes principes que le fichier de test du module 1).</summary>
    private static MemoryStream BuildMinimalPdf(int pageCount)
    {
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>" };
        var kids = string.Join(' ', Enumerable.Range(0, pageCount).Select(i => $"{3 + i} 0 R"));
        objects.Add($"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>");
        for (var i = 0; i < pageCount; i++)
        {
            objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>");
        }

        var sb = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(sb.ToString()));
            sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefStart = Encoding.ASCII.GetByteCount(sb.ToString());
        sb.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets.Skip(1))
        {
            sb.Append($"{offset:D10} 00000 n \n");
        }
        sb.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefStart}\n%%EOF");

        return new MemoryStream(Encoding.ASCII.GetBytes(sb.ToString()));
    }
}
