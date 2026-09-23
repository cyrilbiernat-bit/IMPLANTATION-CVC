namespace Bim.Cvc.Api.Projects;

public sealed record AssistantMessageDto(string Role, string Content);

public sealed record AskAssistantRequest(string Question, IReadOnlyList<AssistantMessageDto>? History);

public sealed record AskAssistantResponse(string Answer);
