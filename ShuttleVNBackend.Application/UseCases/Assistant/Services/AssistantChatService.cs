using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Assistant;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Assistant;

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

        for (var round = 0; round <= _settings.MaxToolRounds; round++)
        {
            var canCallTools = round < _settings.MaxToolRounds;

            var completionRequest = new ChatCompletionRequest(
                messages,
                canCallTools ? toolSpecs : []);

            var accumulatedText = new StringBuilder();
            List<ChatToolCall>? toolCalls = null;

            await foreach (var completionEvent in chatClient.StreamAsync(completionRequest, ct))
            {
                switch (completionEvent)
                {
                    case ChatStreamEvent.TextDelta delta when !string.IsNullOrEmpty(delta.Text):
                        accumulatedText.Append(delta.Text);
                        yield return new AssistantStreamEvent.TextDelta(delta.Text);
                        break;
                    case ChatStreamEvent.ToolCallsReady calls:
                        toolCalls = calls.Calls.ToList();
                        break;
                }
            }

            if (toolCalls is { Count: > 0 })
            {
                // Truncate before building the assistant message: every tool_call must have
                // a matching tool message or providers reject the next round.
                var calls = toolCalls.Take(_settings.MaxToolCallsPerRound).ToList();

                var assistantMessage = new ChatCompletionMessage(
                    "assistant",
                    accumulatedText.Length == 0 ? null : accumulatedText.ToString(),
                    calls);
                messages.Add(assistantMessage);

                foreach (var call in calls)
                {
                    yield return new AssistantStreamEvent.ToolStarted(call.Name);

                    var result = await ExecuteToolAsync(call, ct);
                    messages.Add(new ChatCompletionMessage("tool", result, ToolCallId: call.Id));
                }

                continue;
            }

            reply = AssistantReplyValidator.Sanitize(
                accumulatedText.ToString(),
                _settings.MaxReplyChars);

            break;
        }

        yield return new AssistantStreamEvent.Done(reply);
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

        messages.AddRange(request.Messages.Select(m =>
            new ChatCompletionMessage(m.Role.Trim().ToLowerInvariant(), m.Content.Trim())));

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
