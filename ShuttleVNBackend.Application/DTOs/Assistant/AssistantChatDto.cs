namespace ShuttleVNBackend.Application.DTOs.Assistant;

public sealed record AssistantChatRequest(IReadOnlyList<AssistantChatMessage> Messages);

public sealed record AssistantChatMessage(string Role, string Content);

public sealed record AssistantChatResponse(string Reply);