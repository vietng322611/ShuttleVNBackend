using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Assistant;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Assistant;
using ShuttleVNBackend.Application.UseCases.Assistant.Tools;

namespace ShuttleVNBackend.Application.UseCases.Assistant.Services;

public sealed class AssistantChatService(
    IChatCompletionClient chatClient,
    IEnumerable<IAssistantTool> tools,
    IOptions<AssistantOptions> options,
    TimeProvider clock)
{
    private static readonly JsonSerializerOptions PayloadJson = JsonSerializerOptions.Web;
    private static readonly string[] VietnameseDayNames =
        ["Chủ nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy"];

    // MaxMessages caps what a caller may send; this caps what is forwarded to the model.
    // ponytail: fixed window; make it configurable when the assistant grows a persona.
    private const int ConversationWindow = 6;

    private readonly Dictionary<string, IAssistantTool> _tools = tools.ToDictionary(t => t.Name);
    private readonly AssistantOptions _settings = options.Value;

    public async Task<AssistantChatResponse> ChatAsync(
        AssistantChatRequest request,
        CancellationToken ct = default)
    {
        string reply = AssistantReplyValidator.FallbackReply;

        await foreach (var streamEvent in StreamAsync(request, ct))
        {
            if (streamEvent is AssistantStreamEvent.Done done)
            {
                reply = done.Reply;
            }
        }

        return new AssistantChatResponse(reply);
    }

    public async IAsyncEnumerable<AssistantStreamEvent> StreamAsync(
        AssistantChatRequest request,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ValidateRequest(request);

        var messages = BuildConversation(request);
        var toolSpecs = _tools.Values
            .Select(t => new ChatToolSpec(t.Name, t.Description, t.Parameters))
            .ToList();

        string reply = AssistantReplyValidator.FallbackReply;
        var allowedNumbers = AssistantReplyValidator.CollectEvidence(request.Messages[^1].Content);
        var lookupHappened = false;

        for (var round = 0; round <= _settings.MaxToolRounds; round++)
        {
            var canCallTools = round < _settings.MaxToolRounds;

            // Round 0 forces a tool call so the model cannot answer from memory.
            var completionRequest = new ChatCompletionRequest(
                messages,
                canCallTools ? toolSpecs : [],
                round == 0 ? "required" : "auto");

            var accumulatedText = new StringBuilder();
            List<ChatToolCall>? toolCalls = null;

            await foreach (var completionEvent in chatClient.StreamAsync(completionRequest, ct))
            {
                switch (completionEvent)
                {
                    case ChatStreamEvent.TextDelta delta when !string.IsNullOrEmpty(delta.Text):
                        accumulatedText.Append(delta.Text);
                        break;
                    case ChatStreamEvent.ToolCallsReady calls:
                        toolCalls = calls.Calls.ToList();
                        break;
                }
            }

            if (toolCalls is { Count: > 0 })
            {
                // Every tool_call needs a matching tool message or providers reject the round.
                var calls = toolCalls.Take(_settings.MaxToolCallsPerRound).ToList();

                messages.Add(new ChatCompletionMessage(
                    "assistant",
                    accumulatedText.Length == 0 ? null : accumulatedText.ToString(),
                    calls));

                foreach (var call in calls)
                {
                    if (_tools.TryGetValue(call.Name, out var tool)
                        && tool is IAssistantReplyTool replyTool)
                    {
                        // Server writes the sentence; the model only picks the branch.
                        reply = replyTool.BuildReply(ParseArguments(call.Arguments));
                        yield return new AssistantStreamEvent.TextDelta(reply);
                        yield return new AssistantStreamEvent.Done(reply);
                        yield break;
                    }

                    yield return new AssistantStreamEvent.ToolStarted(call.Name);

                    var result = await ExecuteToolAsync(call, ct);
                    allowedNumbers.UnionWith(AssistantReplyValidator.CollectEvidence(result));
                    lookupHappened = true;
                    messages.Add(new ChatCompletionMessage("tool", result, ToolCallId: call.Id));
                }

                continue;
            }

            // Free text needs a real lookup and no number the server never supplied.
            var candidate = accumulatedText.ToString();
            reply = lookupHappened
                && !AssistantReplyValidator.HasUnsupportedNumbers(candidate, allowedNumbers)
                    ? AssistantReplyValidator.Sanitize(candidate, _settings.MaxReplyChars)
                    : AssistantReplyValidator.FallbackReply;

            break;
        }

        // Validate before emitting so the client never renders text that is retracted later.
        // ponytail: no token-by-token typing; stream per sentence when the UI needs it.
        yield return new AssistantStreamEvent.TextDelta(reply);
        yield return new AssistantStreamEvent.Done(reply);
    }

    /// <summary>
    /// Malformed arguments become an empty object so decision tools fail closed.
    /// </summary>
    private static JsonElement ParseArguments(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return EmptyArguments();
        }

        try
        {
            using var document = JsonDocument.Parse(arguments);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return EmptyArguments();
        }
    }

    private static JsonElement EmptyArguments()
    {
        return JsonSerializer.Deserialize<JsonElement>("{}");
    }

    private async Task<string> ExecuteToolAsync(ChatToolCall call, CancellationToken ct)
    {
        if (!_tools.TryGetValue(call.Name, out var tool))
        {
            return Serialize(new { error = $"Công cụ '{call.Name}' không tồn tại." });
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(call.Arguments);
        }
        catch (JsonException)
        {
            return Serialize(new { error = "Tham số công cụ không phải JSON hợp lệ." });
        }

        using (document)
        {
            var outcome = await tool.ExecuteAsync(document.RootElement, ct);
            return Serialize(outcome.Payload);
        }
    }

    private static string Serialize(object payload)
    {
        return JsonSerializer.Serialize(payload, PayloadJson);
    }

    private void ValidateRequest(AssistantChatRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var messages = request.Messages;

        if (messages is null || messages.Count == 0)
        {
            errors["Messages"] = ["Messages is required."];
        }
        else if (messages.Count > _settings.MaxMessages)
        {
            errors["Messages"] = [$"At most {_settings.MaxMessages} messages are allowed."];
        }

        if (errors.Count == 0)
        {
            for (var index = 0; index < messages!.Count; index++)
            {
                var message = messages[index];
                if (message is null)
                {
                    errors[$"Messages[{index}]"] = ["Message is required."];
                    continue;
                }

                var role = message.Role?.Trim().ToLowerInvariant();

                if (role is not ("user" or "assistant"))
                {
                    errors[$"Messages[{index}].Role"] = ["Role must be 'user' or 'assistant'."];
                    continue;
                }

                var content = message.Content?.Trim();
                if (string.IsNullOrEmpty(content))
                {
                    errors[$"Messages[{index}].Content"] = ["Message content is required."];
                }
                else if (content.Length > _settings.MaxMessageLength)
                {
                    errors[$"Messages[{index}].Content"] =
                        [$"Message content must be at most {_settings.MaxMessageLength} characters."];
                }
            }

            if (messages![^1]?.Role?.Trim().ToLowerInvariant() != "user")
            {
                errors["Messages"] = ["The last message must be from the user."];
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors: errors);
        }
    }

    private List<ChatCompletionMessage> BuildConversation(AssistantChatRequest request)
    {
        var messages = new List<ChatCompletionMessage>
        {
            new("system", $"{AssistantPrompt.System}\n\n{BuildCurrentTimeNote()}")
        };

        messages.AddRange(request.Messages
            .TakeLast(ConversationWindow)
            .Select(m => new ChatCompletionMessage(m.Role.Trim().ToLowerInvariant(), m.Content.Trim())));

        return messages;
    }

    private string BuildCurrentTimeNote()
    {
        var now = VietnamTime.NowLocal(clock);
        var dayName = VietnameseDayNames[(int)now.DayOfWeek];
        var timestamp = now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        return $"Bây giờ là {dayName}, {timestamp} (giờ Việt Nam). " +
               "Khi gọi get_court_availability, đổi 'hôm nay', 'ngày mai', 'thứ Bảy này'… thành ngày yyyy-MM-dd theo thời điểm này. " +
               "Chỉ gợi ý các khung giờ từ thời điểm hiện tại trở đi.";
    }
}
