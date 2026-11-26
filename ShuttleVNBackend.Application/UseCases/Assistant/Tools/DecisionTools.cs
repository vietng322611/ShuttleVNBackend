using System.Text.Json;
using ShuttleVNBackend.Application.Interfaces.Assistant;

namespace ShuttleVNBackend.Application.UseCases.Assistant.Tools;

/// <summary>
/// Tool whose user-facing sentence is written by the harness: the model picks the branch,
/// never the wording, so injection cannot reshape a refusal or a policy answer.
/// </summary>
public interface IAssistantReplyTool : IAssistantTool
{
    string BuildReply(JsonElement arguments);
}

/// <summary>
/// Answers a known FAQ topic from the server catalog.
/// </summary>
public sealed class AnswerFaqTool : IAssistantReplyTool
{
    private static readonly object Schema = new
    {
        type = "object",
        properties = new
        {
            topic = new
            {
                type = "string",
                @enum = AssistantFaq.Topics,
                description = "Chủ đề câu hỏi thường gặp của người dùng."
            }
        },
        required = new[] { "topic" },
        additionalProperties = false
    };

    public string Name => "answer_faq";

    public string Description =>
        "Trả lời câu hỏi thường gặp về ShuttleVN (giờ mở cửa, slot, giá, hủy/đổi, cách đặt, thanh toán, liên hệ nhân viên). " +
        "Bắt buộc gọi công cụ này khi câu hỏi thuộc các chủ đề đó.";

    public object Parameters => Schema;

    public Task<ToolOutcome> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        return Task.FromResult(new ToolOutcome(false, new { topic = ReadTopic(arguments) }));
    }

    public string BuildReply(JsonElement arguments) => AssistantFaq.Answer(ReadTopic(arguments));

    private static string? ReadTopic(JsonElement arguments)
    {
        return arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("topic", out var topic)
            && topic.ValueKind == JsonValueKind.String
                ? topic.GetString()
                : null;
    }
}

/// <summary>
/// The only path to a refusal: the semantic decision carries a schema instead of relying
/// on keyword matching that any phrasing can dodge.
/// </summary>
public sealed class DeclineOutOfScopeTool : IAssistantReplyTool
{
    private static readonly object Schema = new
    {
        type = "object",
        properties = new
        {
            reason = new
            {
                type = "string",
                @enum = new[] { "out_of_scope", "unsupported_action", "greeting" },
                description =
                    "out_of_scope: không liên quan sân cầu lông ShuttleVN. " +
                    "unsupported_action: muốn đặt/hủy/thanh toán/đọc dữ liệu cá nhân. " +
                    "greeting: chỉ chào hỏi."
            }
        },
        required = new[] { "reason" },
        additionalProperties = false
    };

    public string Name => "decline_out_of_scope";

    public string Description =>
        "Từ chối khi câu hỏi ngoài phạm vi sân cầu lông ShuttleVN, khi người dùng đòi thao tác nghiệp vụ " +
        "hoặc dữ liệu cá nhân, hoặc khi chỉ chào hỏi xã giao. Bắt buộc gọi công cụ này thay vì tự trả lời.";

    public object Parameters => Schema;

    public Task<ToolOutcome> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        return Task.FromResult(new ToolOutcome(false, new { declined = true }));
    }

    public string BuildReply(JsonElement arguments) => ReadReason(arguments) switch
    {
        "greeting" =>
            "Chào bạn! Mình là trợ lý của ShuttleVN. Bạn muốn hỏi về giá sân, khung giờ trống hay cách đặt sân không?",
        "unsupported_action" =>
            "Mình chỉ tư vấn, không thực hiện đặt, hủy hay thanh toán. " +
            "Bạn dùng màn Đặt sân / Lịch sử đặt sân trên website hoặc liên hệ nhân viên để được hỗ trợ.",
        _ =>
            "Mình chỉ tư vấn về sân cầu lông ShuttleVN: giá, khung giờ trống, cách đặt sân và các câu hỏi thường gặp. " +
            "Bạn muốn hỏi gì về sân không?"
    };

    private static string? ReadReason(JsonElement arguments)
    {
        return arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("reason", out var reason)
            && reason.ValueKind == JsonValueKind.String
                ? reason.GetString()
                : null;
    }
}
