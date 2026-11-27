namespace ShuttleVNBackend.Application.Interfaces.Assistant;

public sealed record ChatToolSpec(string Name, string Description, object Parameters);

public sealed record ChatToolCall(string Id, string Name, string Arguments);

public sealed record ChatCompletionMessage(
    string Role,
    string? Content,
    IReadOnlyList<ChatToolCall>? ToolCalls = null,
    string? ToolCallId = null);

public sealed record ChatCompletionRequest(
    IReadOnlyList<ChatCompletionMessage> Messages,
    IReadOnlyList<ChatToolSpec> Tools,
    string ToolChoice = "auto");

public abstract record ChatStreamEvent
{
    public sealed record TextDelta(string Text) : ChatStreamEvent;

    public sealed record ToolCallsReady(IReadOnlyList<ChatToolCall> Calls) : ChatStreamEvent;
}

public interface IChatCompletionClient
{
    IAsyncEnumerable<ChatStreamEvent> StreamAsync(ChatCompletionRequest request, CancellationToken ct);
}