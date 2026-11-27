using System.Text.Json;
using ShuttleVNBackend.Application.Interfaces.Assistant;
using ShuttleVNBackend.Application.UseCases.Courts.Services;

namespace ShuttleVNBackend.Application.UseCases.Assistant.Tools;

public sealed class ListCourtsTool(CourtService courtService) : IAssistantTool
{
    private static readonly object Schema = new
    {
        type = "object",
        properties = new { },
        additionalProperties = false
    };

    public string Name => "list_courts";

    public string Description =>
        "Liệt kê các sân cầu lông đang sử dụng kèm tên, mô tả và trạng thái.";

    public object Parameters => Schema;

    public async Task<ToolOutcome> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var courts = await courtService.GetCourtsAsync(ct);

        var payload = new ListCourtsToolResult(
            courts.Items
                .Select(c => new AssistantCourtDto(c.CourtId, c.Name, c.Description, c.Status))
                .ToList());

        return new ToolOutcome(false, payload);
    }
}