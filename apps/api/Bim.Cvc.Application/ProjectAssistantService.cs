using System.Text;

namespace Bim.Cvc.Application;

public sealed class AssistantUnavailableException(string message) : Exception(message);

/// <summary>Un tour de conversation. Role vaut "user" ou "assistant".</summary>
public sealed record AssistantMessage(string Role, string Content);

/// <summary>
/// Abstrait l'appel au modèle de langage pour que l'Application ne dépende
/// pas du SDK Anthropic — c'est l'Infrastructure qui l'implémente (voir
/// <c>AnthropicAssistantChatClient</c>), à l'image d'<see cref="IIfcGeometryExtractor"/>
/// pour l'extraction IFC.
/// </summary>
public interface IAssistantChatClient
{
    Task<string> AskAsync(string systemPrompt, IReadOnlyList<AssistantMessage> messages, CancellationToken ct = default);
}

/// <summary>
/// Lot 2 — assistant conversationnel : répond en langage naturel à des
/// questions sur un projet, à partir des mêmes données déjà en base que les
/// modules 5 (métrés) et 6 (nomenclature). Ne vérifie aucune règle
/// métier/réglementaire et ne modifie jamais le projet — il ne fait que lire
/// et répondre.
/// </summary>
public sealed class ProjectAssistantService(
    ProjectService projects,
    ProjectMetresService metresService,
    ProjectNomenclatureService nomenclatureService,
    IAssistantChatClient chatClient)
{
    // Borne le prompt envoyé au modèle : au-delà, le coût et la latence par
    // question deviennent excessifs pour un gros projet. Les questions
    // portant sur les objets au-delà de cette limite resteront sans réponse
    // précise — limite MVP documentée plutôt que cachée.
    private const int MaxNomenclatureRowsInPrompt = 200;

    public async Task<string> AskAsync(
        Guid projectId, IReadOnlyList<AssistantMessage> history, string question, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            throw new ArgumentException("La question ne peut pas être vide.", nameof(question));
        }

        var project = projects.Get(projectId); // lève ProjectNotFoundException si le projet n'existe pas.
        var metres = metresService.Compute(projectId);
        var nomenclature = nomenclatureService.Compute(projectId);

        var systemPrompt = BuildSystemPrompt(project.Name, metres, nomenclature);
        var messages = new List<AssistantMessage>(history) { new("user", question) };

        return await chatClient.AskAsync(systemPrompt, messages, ct);
    }

    private static string BuildSystemPrompt(
        string projectName, ProjectMetresSummary metres, IReadOnlyList<NomenclatureRow> nomenclature)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            "Tu es l'assistant technique intégré à un logiciel d'ingénierie CVC " +
            "(chauffage, ventilation, climatisation, protection incendie). Réponds en " +
            "français, de façon concise et précise, exclusivement à partir des données " +
            "du projet fournies ci-dessous. Si une information n'y figure pas, dis-le " +
            "clairement plutôt que de l'inventer. Tu ne peux pas modifier le projet : tu " +
            "ne fais que répondre à des questions.");
        sb.AppendLine();
        sb.AppendLine($"Projet : {projectName}");
        sb.AppendLine($"Nombre de plans : {metres.DrawingCount}");
        sb.AppendLine($"Longueur totale de gaines : {metres.TotalDuctLengthMeters:0.##} m");
        sb.AppendLine($"Surface totale à calorifuger : {metres.TotalInsulationAreaM2:0.##} m²");
        sb.AppendLine($"Poids total du réseau : {metres.TotalWeightKg:0.##} kg");

        if (metres.AccessoryCounts.Count > 0)
        {
            sb.AppendLine("Accessoires et terminaux :");
            foreach (var a in metres.AccessoryCounts)
            {
                sb.AppendLine($"- {a.Type} : {a.Count}");
            }
        }

        sb.AppendLine();
        if (nomenclature.Count == 0)
        {
            sb.AppendLine("Aucun objet CVC n'a encore été posé sur les plans de ce projet.");
        }
        else
        {
            sb.AppendLine("Nomenclature détaillée (une ligne par objet posé sur un plan) :");
            foreach (var row in nomenclature.Take(MaxNomenclatureRowsInPrompt))
            {
                sb.Append($"- [{row.ObjectId.ToString()[..8]}] {row.Type} sur \"{row.DrawingFileName}\" (calque \"{row.LayerName}\")");
                if (row.DiameterMm is { } diameter) sb.Append($", Ø{diameter:0.##} mm");
                if (row.WidthMm is { } w && row.HeightMm is { } h) sb.Append($", {w:0.##}×{h:0.##} mm");
                if (row.LengthMeters is { } len) sb.Append($", longueur {len:0.##} m");
                if (row.DebitM3h is { } debit) sb.Append($", débit {debit:0.##} m³/h");
                if (row.VitesseMs is { } vitesse) sb.Append($", vitesse {vitesse:0.##} m/s");
                if (row.PressionPa is { } pression) sb.Append($", pression {pression:0.##} Pa");
                if (row.WeightKg is { } weight) sb.Append($", poids {weight:0.##} kg");
                if (row.InsulationAreaM2 is { } area) sb.Append($", surface calorifuge {area:0.##} m²");
                sb.AppendLine();
            }

            if (nomenclature.Count > MaxNomenclatureRowsInPrompt)
            {
                sb.AppendLine(
                    $"... et {nomenclature.Count - MaxNomenclatureRowsInPrompt} objet(s) supplémentaire(s) non " +
                    "détaillé(s) ici (projet trop volumineux pour être listé intégralement) — préviens " +
                    "l'utilisateur si sa question en dépend.");
            }
        }

        return sb.ToString();
    }
}
