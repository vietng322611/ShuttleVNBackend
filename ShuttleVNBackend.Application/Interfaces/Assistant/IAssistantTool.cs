using System.Text.Json;

namespace ShuttleVNBackend.Application.Interfaces.Assistant;

public sealed record ToolOutcome(bool IsError, object Payload);

public interface IAssistantTool
{
    string Name { get; }
    string Description { get; }
    object Parameters { get; }

    Task<ToolOutcome> ExecuteAsync(JsonElement arguments, CancellationToken ct);
}