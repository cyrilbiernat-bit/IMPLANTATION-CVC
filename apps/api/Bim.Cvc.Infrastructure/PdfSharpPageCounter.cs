using Bim.Cvc.Application;
using PdfSharp.Pdf.IO;

namespace Bim.Cvc.Infrastructure;

public sealed class PdfSharpPageCounter : IPdfPageCounter
{
    public int CountPages(Stream pdfContent)
    {
        pdfContent.Position = 0;
        using var document = PdfReader.Open(pdfContent, PdfDocumentOpenMode.Import);
        return document.PageCount;
    }
}
