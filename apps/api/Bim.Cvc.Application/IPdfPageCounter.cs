namespace Bim.Cvc.Application;

public interface IPdfPageCounter
{
    int CountPages(Stream pdfContent);
}
