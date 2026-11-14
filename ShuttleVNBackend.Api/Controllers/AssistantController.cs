using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShuttleVNBackend.Api.Common;
using ShuttleVNBackend.Api.DTOs.Common;
using ShuttleVNBackend.Application.DTOs.Assistant;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.UseCases.Assistant.Services;

namespace ShuttleVNBackend.Api.Controllers;

[ApiController]
[Route("api/assistant")]
[AllowAnonymous]
public class AssistantController(AssistantChatService assistantChatService) : ControllerBase
{
    private static readonly JsonSerializerOptions StreamJson = new(JsonSerializerDefaults.Web);

    private const string AssistantUnavailableMessage =
        "Trợ lý tạm thời không phản hồi. Vui lòng thử lại sau.";

    [HttpPost("chat")]
    [EnableRateLimiting(AssistantRateLimit.PolicyName)]
    public async Task<ActionResult<ApiResponse<AssistantChatResponse>>> Chat(
        [FromBody] AssistantChatRequest request,
        CancellationToken ct)
    {
        var response = await assistantChatService.ChatAsync(request, ct);
        return Ok(ApiResponseFactory.Success(response));
    }

    [HttpPost("chat/stream")]
    [EnableRateLimiting(AssistantRateLimit.PolicyName)]
    public async Task<IActionResult> ChatStream(
        [FromBody] AssistantChatRequest request,
        CancellationToken ct)
    {
        try
        {
            await foreach (var streamEvent in assistantChatService.StreamAsync(request, ct))
            {
                if (!Response.HasStarted)
                {
                    Response.ContentType = "text/event-stream";
                    Response.Headers.CacheControl = "no-cache";
                    Response.Headers["X-Accel-Buffering"] = "no";
                }

                var (name, data) = Map(streamEvent);
                await WriteEventAsync(name, data, ct);
            }
        }
        catch (AppException ex) when (!Response.HasStarted)
        {
            // Failures before the first byte keep the regular JSON error envelope.
            Response.ContentType = "application/json";
            Response.StatusCode = ex.StatusCode;

            IDictionary<string, string[]> errors = ex is ValidationException validation
                ? validation.Errors
                : new Dictionary<string, string[]>();

            await Response.WriteAsJsonAsync(
                ApiResponseFactory.Failure<object>(ex.Message, errors), ct);
        }
        catch (AppException ex)
        {
            await WriteEventAsync(
                "error",
                JsonSerializer.Serialize(new { code = ex.Code, message = ex.Message }, StreamJson), ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Upstream stalled and hit the configured timeout mid-stream.
            await WriteEventAsync("error", JsonSerializer.Serialize(new { code = "ASSISTANT_UNAVAILABLE", message = AssistantUnavailableMessage },
                    StreamJson), ct);
        }

        return new EmptyResult();
    }

    private static (string Name, string Data) Map(AssistantStreamEvent streamEvent)
    {
        return streamEvent switch
        {
            AssistantStreamEvent.ToolStarted tool =>
                ("tool", JsonSerializer.Serialize(new { name = tool.Name, status = "done" }, StreamJson)),
            AssistantStreamEvent.TextDelta delta =>
                ("delta", JsonSerializer.Serialize(new { text = delta.Text }, StreamJson)),
            AssistantStreamEvent.Done done =>
                ("done", JsonSerializer.Serialize(new { reply = done.Reply }, StreamJson)),
            _ => throw new InvalidOperationException("Unsupported assistant stream event.")
        };
    }

    private async Task WriteEventAsync(string name, string data, CancellationToken ct)
    {
        await Response.WriteAsync($"event: {name}\ndata: {data}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
