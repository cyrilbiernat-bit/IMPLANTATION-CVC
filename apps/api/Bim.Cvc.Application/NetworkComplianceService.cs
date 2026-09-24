using Bim.Cvc.Domain;

namespace Bim.Cvc.Application;

public enum ComplianceSeverity
{
    Warning,
    Critical,
}

public sealed record ComplianceFinding(
    Guid ObjectId,
    string DrawingFileName,
    CvcObjectType Type,
    ComplianceSeverity Severity,
    string RuleCode,
    string Message,
    double Value);

/// <summary>
/// Lot 2 — vérification réglementaire automatique : contrôle des gaines déjà
/// dessinées contre des règles usuelles de bonne pratique aéraulique
/// (confort acoustique, pertes de charge), pas un dimensionnement complet
/// (hors périmètre du MVP — il n'y a pas de solveur de réseau aéraulique).
/// Les seuils sont ceux d'un réseau de ventilation confort (bureaux,
/// logements) ; un réseau industriel ou une centrale de traitement d'air
/// tolère des vitesses plus élevées et devrait les revoir à la hausse.
/// </summary>
public sealed class NetworkComplianceService(
    ProjectService projects,
    IDrawingRepository drawings,
    ICvcObjectRepository objects)
{
    private const double VelocityWarningMs = 6.0;
    private const double VelocityCriticalMs = 10.0;
    private const double MaxAspectRatio = 4.0;

    public IReadOnlyList<ComplianceFinding> Check(Guid projectId)
    {
        _ = projects.Get(projectId); // lève ProjectNotFoundException si le projet n'existe pas.

        var findings = new List<ComplianceFinding>();
        foreach (var drawing in drawings.GetByProject(projectId))
        {
            foreach (var obj in objects.GetByDrawing(drawing.Id))
            {
                if (!CvcObject.IsDuct(obj.Type)) continue;

                CheckVelocity(obj, drawing.FileName, findings);
                if (obj.Type == CvcObjectType.GaineRectangulaire)
                {
                    CheckAspectRatio(obj, drawing.FileName, findings);
                }
            }
        }

        return findings;
    }

    private static void CheckVelocity(CvcObject obj, string drawingFileName, List<ComplianceFinding> findings)
    {
        var velocity = EffectiveVelocityMs(obj);
        if (velocity is not { } v) return;

        if (v > VelocityCriticalMs)
        {
            findings.Add(new ComplianceFinding(
                obj.Id, drawingFileName, obj.Type, ComplianceSeverity.Critical, "VitesseExcessive",
                $"Vitesse estimée {v:0.#} m/s — nettement au-dessus du seuil usuel de confort acoustique " +
                $"({VelocityCriticalMs:0} m/s). Risque de bruit et de perte de charge important.",
                v));
        }
        else if (v > VelocityWarningMs)
        {
            findings.Add(new ComplianceFinding(
                obj.Id, drawingFileName, obj.Type, ComplianceSeverity.Warning, "VitesseElevee",
                $"Vitesse estimée {v:0.#} m/s — au-dessus du seuil usuel de confort acoustique ({VelocityWarningMs:0} m/s).",
                v));
        }
    }

    private static void CheckAspectRatio(CvcObject obj, string drawingFileName, List<ComplianceFinding> findings)
    {
        if (obj.WidthMm is not { } w || obj.HeightMm is not { } h || w <= 0 || h <= 0) return;

        var ratio = Math.Max(w, h) / Math.Min(w, h);
        if (ratio > MaxAspectRatio)
        {
            findings.Add(new ComplianceFinding(
                obj.Id, drawingFileName, obj.Type, ComplianceSeverity.Warning, "RapportAspectExcessif",
                $"Rapport largeur/hauteur {ratio:0.#}:1 — au-delà de {MaxAspectRatio:0}:1, les pertes de charge " +
                "augmentent et le calorifugeage devient difficile.",
                ratio));
        }
    }

    /// <summary>Vitesse mesurée si saisie, sinon déduite du débit et de la section — null si ni l'un ni l'autre n'est disponible.</summary>
    private static double? EffectiveVelocityMs(CvcObject obj)
    {
        if (obj.VitesseMs is { } v) return v;
        if (obj.DebitM3h is not { } debit) return null;

        var sectionM2 = SectionM2(obj);
        return sectionM2 is > 0 ? debit / 3600.0 / sectionM2 : null;
    }

    private static double? SectionM2(CvcObject obj) => obj.Type switch
    {
        CvcObjectType.GaineRectangulaire when obj.WidthMm is { } w && obj.HeightMm is { } h => (w / 1000.0) * (h / 1000.0),
        CvcObjectType.GaineCirculaire when obj.DiameterMm is { } d => Math.PI * Math.Pow(d / 1000.0 / 2, 2),
        _ => null,
    };
}
