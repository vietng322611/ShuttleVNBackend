using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ShuttleVNBackend.Application.Interfaces.Assistant;

namespace ShuttleVNBackend.Infrastructure.Assistant;

/// <summary>
///     Reassembles streamed tool calls. OpenAI streams `arguments` in fragments grouped by index.
/// </summary>
internal sealed class ToolCallAccumulator
{
    private readonly ConcurrentDictionary<int, Fragment> _calls = new();

    public bool HasCalls => !_calls.IsEmpty;

    public void Read(JsonElement delta)
    {
        if (!delta.TryGetProperty("tool_calls", out var toolCalls)
            || toolCalls.ValueKind != JsonValueKind.Array)
            return;

        foreach (var call in toolCalls.EnumerateArray())
        {
            var index = call.TryGetProperty("index", out var indexElement)
                        && indexElement.TryGetInt32(out var parsed)
                ? parsed
                : 0;

            var fragment = _calls.GetOrAdd(index, _ => new Fragment());

            if (call.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                fragment.Id = id.GetString();

            if (!call.TryGetProperty("function", out var function)
                || function.ValueKind != JsonValueKind.Object)
                continue;

            if (function.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                fragment.Name = name.GetString();

            if (function.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.String)
                fragment.Arguments.Append(args.GetString());
        }
    }

    public IReadOnlyList<ChatToolCall> Build()
    {
        return _calls
            .OrderBy(pair => pair.Key)
            .Select(pair => new ChatToolCall(
                pair.Value.Id ?? $"call_{pair.Key}",
                pair.Value.Name ?? string.Empty,
                pair.Value.Arguments.ToString()))
            .ToList();
    }

    private sealed class Fragment
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public StringBuilder Arguments { get; } = new();
    }
}