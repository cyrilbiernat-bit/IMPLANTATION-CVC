using Bim.Cvc.Application;
using Bim.Cvc.Domain;
using PdfSharp.Pdf.IO;

namespace Bim.Cvc.Infrastructure;

public sealed class PdfPlanParser : IPlanFileParser
{
    public bool CanParse(PlanFormat format) => format == PlanFormat.Pdf;

    public ParsedPlan Parse(PlanFormat format, Stream content)
    {
        content.Position = 0;
        using var document = PdfReader.Open(content, PdfDocumentOpenMode.Import);
        return new ParsedPlan(document.PageCount, Entities: null);
    }
}
