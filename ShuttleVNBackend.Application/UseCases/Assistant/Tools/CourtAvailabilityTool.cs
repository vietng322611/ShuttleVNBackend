using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Courts;
using ShuttleVNBackend.Application.Interfaces.Assistant;
using ShuttleVNBackend.Application.UseCases.Courts.Services;

namespace ShuttleVNBackend.Application.UseCases.Assistant.Tools;

public sealed class CourtAvailabilityTool(
    CourtService courtService,
    TimeProvider clock,
    IOptions<AssistantOptions> options) : IAssistantTool
{
    private static readonly object Schema = new
    {
        type = "object",
        properties = new
        {
            date = new
            {
                type = "string",
                format = "date",
                description = "Ngày cần xem theo giờ Việt Nam, định dạng YYYY-MM-DD."
            }
        },
        required = new[] { "date" },
        additionalProperties = false
    };

    public string Name => "get_court_availability";

    public string Description =>
        "Xem tình trạng slot 30 phút (trống, đã đặt, đóng) và giá mỗi giờ của từng sân trong một ngày.";

    public object Parameters => Schema;

    public async Task<ToolOutcome> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty("date", out var dateElement)
            || dateElement.ValueKind != JsonValueKind.String
            || !DateOnly.TryParseExact(
                dateElement.GetString(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            return Error("`date` là bắt buộc và phải có định dạng YYYY-MM-DD.");

        var today = VietnamTime.TodayLocal(clock);
        var horizon = today.AddDays(options.Value.AvailabilityHorizonDays);

        if (date < today || date > horizon)
            return Error(
                $"`date` phải nằm trong khoảng {today:yyyy-MM-dd} đến {horizon:yyyy-MM-dd}.");

        var grid = await courtService.GetCourtGridAsync(date, ct);
        var now = VietnamTime.NowLocal(clock);

        var payload = new CourtAvailabilityToolResult(
            grid.Date,
            grid.Courts
                .Select(court => new AssistantCourtAvailabilityDto(
                    court.Court.CourtId,
                    court.Court.Name,
                    court.Slots
                        .Where(slot => IsBookable(slot, date, now))
                        .Select(slot => new AssistantSlotDto(
                            slot.StartTime, slot.EndTime, slot.DisplayStatus, slot.PricePerHour))
                        .ToList()))
                .ToList());

        return new ToolOutcome(false, payload);
    }

    /// <summary>
    ///     Free slots the customer can still book. Booked and closed slots only inflate the
    ///     payload, and a slot that already ended is noise for the model.
    /// </summary>
    private static bool IsBookable(CourtSlotDto slot, DateOnly date, DateTime now)
    {
        if (slot.DisplayStatus != "AVAILABLE") return false;

        return date > DateOnly.FromDateTime(now)
               || TimeOnly.ParseExact(slot.EndTime, "HH:mm", CultureInfo.InvariantCulture)
               > TimeOnly.FromDateTime(now);
    }

    private static ToolOutcome Error(string message)
    {
        return new ToolOutcome(true, new AssistantToolError(message));
    }
}