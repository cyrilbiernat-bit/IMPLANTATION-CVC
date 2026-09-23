using Bim.Cvc.Application;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bim.Cvc.Infrastructure.Tests;

public sealed class AnthropicAssistantChatClientTests
{
    [Fact]
    public async Task AskAsync_throws_AssistantUnavailableException_when_no_api_key_is_configured()
    {
        var client = new AnthropicAssistantChatClient(Options.Create(new AssistantOptions { ApiKey = null }));

        var ex = await Assert.ThrowsAsync<AssistantUnavailableException>(
            () => client.AskAsync("system", [new AssistantMessage("user", "Bonjour ?")]));
        Assert.Contains("Assistant:ApiKey", ex.Message);
    }

    [Fact]
    public async Task AskAsync_throws_AssistantUnavailableException_when_the_api_key_is_blank()
    {
        var client = new AnthropicAssistantChatClient(Options.Create(new AssistantOptions { ApiKey = "   " }));

        await Assert.ThrowsAsync<AssistantUnavailableException>(
            () => client.AskAsync("system", [new AssistantMessage("user", "Bonjour ?")]));
    }

    [Fact(Skip = "Nécessite une vraie clé API Anthropic (variable d'environnement ANTHROPIC_API_KEY_FOR_TESTS) — non disponible dans cet environnement de build.")]
    public async Task AskAsync_gets_a_real_answer_from_the_live_Anthropic_API()
    {
        var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY_FOR_TESTS");
        var client = new AnthropicAssistantChatClient(Options.Create(new AssistantOptions { ApiKey = apiKey }));

        var answer = await client.AskAsync(
            "Tu es un assistant de test. Réponds uniquement par le mot OK.",
            [new AssistantMessage("user", "Confirme.")]);

        Assert.Contains("OK", answer, StringComparison.OrdinalIgnoreCase);
    }
}
