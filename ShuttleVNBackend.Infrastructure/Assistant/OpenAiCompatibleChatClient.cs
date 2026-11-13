using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Assistant;
using ShuttleVNBackend.Application.UseCases.Assistant;

namespace ShuttleVNBackend.Infrastructure.Assistant;

public sealed class OpenAiCompatibleChatClient(
    HttpClient httpClient,
    IOptions<AssistantOptions> options,
    ILogger<OpenAiCompatibleChatClient> logger) : IChatCompletionClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AssistantOptions _settings = options.Value;
    private readonly JsonObject _extraBody = ParseExtraBody(options.Value.ExtraBody);

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_settings.BaseUrl)
            || string.IsNullOrWhiteSpace(_settings.Model)
            || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new AssistantUnavailableException();
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

        HttpResponseMessage? response = null;
        Stream? body = null;

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_settings.BaseUrl.TrimEnd('/')}/chat/completions");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            httpRequest.Content = JsonContent.Create(BuildPayload(request), options: Json);

            response = await httpClient.SendAsync(
                httpRequest, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Upstream AI provider returned non-success status code: {StatusCode}", (int)response.StatusCode);
                throw new AssistantUnavailableException();
            }

            body = await response.Content.ReadAsStreamAsync(timeout.Token);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or OperationCanceledException
            && !ct.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Upstream AI provider request failed with error: {ErrorType}", exception.GetType().Name);
            response?.Dispose();
            throw new AssistantUnavailableException();
        }

        using (response)
        {
            using var reader = new StreamReader(body!, Encoding.UTF8);

            var accumulator = new ToolCallAccumulator();

            while (await ReadEventDataAsync(reader, timeout.Token) is { } data)
            {
                if (data == "[DONE]")
                {
                    break;
                }

                ChatStreamEvent? emitted = null;
                try
                {
                    emitted = ParseChunk(data, accumulator);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (emitted is not null)
                {
                    yield return emitted;
                }
            }

            if (accumulator.HasCalls)
            {
                yield return new ChatStreamEvent.ToolCallsReady(accumulator.Build());
            }
        }
    }

    private JsonObject BuildPayload(ChatCompletionRequest request)
    {
        var payload = JsonSerializer.SerializeToNode(new
        {
            model = _settings.Model,
            stream = true,
            temperature = _settings.Temperature,
            max_tokens = _settings.MaxTokens,
            messages = request.Messages.Select(ToPayloadMessage).ToList(),
            tools = request.Tools.Count == 0
                ? null
                : request.Tools.Select(t => new
                {
                    type = "function",
                    function = new { name = t.Name, description = t.Description, parameters = t.Parameters }
                }).ToList(),
            tool_choice = request.Tools.Count == 0 ? null : "auto"
        }, Json)!.AsObject();

        foreach (var (key, value) in _extraBody)
        {
            payload[key] = value?.DeepClone();
        }

        return payload;
    }

    private static JsonObject ParseExtraBody(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(json) is JsonObject body ? body : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static object ToPayloadMessage(ChatCompletionMessage message)
    {
        return new
        {
            role = message.Role,
            content = message.Content,
            tool_call_id = message.ToolCallId,
            tool_calls = message.ToolCalls?.Select(c => new
            {
                id = c.Id,
                type = "function",
                function = new { name = c.Name, arguments = c.Arguments }
            }).ToList()
        };
    }

    private static ChatStreamEvent? ParseChunk(string data, ToolCallAccumulator accumulator)
    {
        var chunk = JsonDocument.Parse(data);
        using (chunk)
        {
            var root = chunk.RootElement;

            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return null;
            }

            var choice = choices[0];

            if (!choice.TryGetProperty("delta", out var delta))
            {
                return null;
            }

            accumulator.Read(delta);

            if (delta.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String
                && content.GetString() is { Length: > 0 } text)
            {
                return new ChatStreamEvent.TextDelta(text);
            }

            return null;
        }
    }

    private static async Task<string?> ReadEventDataAsync(StreamReader reader, CancellationToken ct)
    {
        var builder = new StringBuilder();

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0)
            {
                if (builder.Length > 0)
                {
                    return builder.ToString();
                }

                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var payload = line[5..].TrimStart();
                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(payload);
            }
        }

        return builder.Length > 0 ? builder.ToString() : null;
    }
}
