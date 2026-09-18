using System.Reflection;
using System.Runtime.CompilerServices;
using PdfSharp.Fonts;

namespace Bim.Cvc.Api.Projects;

/// <summary>
/// Fournit à PDFsharp la police Liberation Sans (SIL Open Font License,
/// équivalent métrique libre d'Arial) embarquée dans l'assembly, plutôt que
/// de dépendre de polices installées sur le système hôte — le serveur
/// d'API n'a aucune garantie d'avoir des polices système (ex. conteneur
/// minimal), contrairement à ce poste de développement.
/// </summary>
public sealed class EmbeddedFontResolver : IFontResolver
{
    private const string RegularFace = "LiberationSans";
    private const string BoldFace = "LiberationSans#Bold";

    [ModuleInitializer]
    internal static void Register() => GlobalFontSettings.FontResolver = new EmbeddedFontResolver();

    public string DefaultFontName => RegularFace;

    public byte[] GetFont(string faceName)
    {
        var resourceName = faceName == BoldFace
            ? "Bim.Cvc.Api.Projects.Fonts.LiberationSans-Bold.ttf"
            : "Bim.Cvc.Api.Projects.Fonts.LiberationSans-Regular.ttf";

        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Police embarquée introuvable : {resourceName}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(isBold ? BoldFace : RegularFace);
}
