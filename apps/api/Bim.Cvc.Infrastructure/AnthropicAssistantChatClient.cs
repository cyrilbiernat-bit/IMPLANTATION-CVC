using Anthropic;
using Anthropic.Models.Messages;
using Bim.Cvc.Application;
using Microsoft.Extensions.Options;

namespace Bim.Cvc.Infrastructure;

public sealed class AssistantOptions
{
    /// <summary>Clé API Anthropic (https://console.anthropic.com). Aucune valeur par défaut — jamais commitée.</summary>
    public string? ApiKey { get; set; }

    public string Model { get; set; } = "claude-opus-5";
}

/// <summary>
/// Lot 2 — appelle l'API Anthropic (Claude) pour l'assistant conversationnel.
/// Sans clé API configurée, l'assistant est indisponible plutôt que de
/// planter au démarrage : seule une question posée échoue, avec un message
/// clair (voir <see cref="AssistantUnavailableException"/>).
/// </summary>
public sealed class AnthropicAssistantChatClient(IOptions<AssistantOptions> options) : IAssistantChatClient
{
    public async Task<string> AskAsync(
        string systemPrompt, IReadOnlyList<AssistantMessage> messages, CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            throw new AssistantUnavailableException(
                "L'assistant IA n'est pas configuré sur ce serveur (clé API Anthropic manquante — Assistant:ApiKey).");
        }

        AnthropicClient client = new() { ApiKey = opts.ApiKey };

        Message response;
        try
        {
            response = await client.Messages.Create(new MessageCreateParams
            {
                Model = opts.Model,
                MaxTokens = 2048,
                System = systemPrompt,
                Messages = messages
                    .Select(m => new MessageParam
                    {
                        Role = m.Role == "assistant" ? Role.Assistant : Role.User,
                        Content = m.Content,
                    })
                    .ToList(),
            }, ct);
        }
        catch (Exception ex) when (ex is not AssistantUnavailableException)
        {
            throw new AssistantUnavailableException($"Échec de l'appel à l'assistant IA : {ex.Message}");
        }

        var text = response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text).FirstOrDefault();
        return text ?? "";
    }
}
