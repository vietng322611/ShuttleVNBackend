namespace ShuttleVNBackend.Application.UseCases.Assistant.Tools;

public sealed record AssistantCourtDto(int CourtId, string Name, string Description, string Status);

public sealed record ListCourtsToolResult(IReadOnlyList<AssistantCourtDto> Courts);

public sealed record AssistantSlotDto(string Start, string End, string Status, decimal PricePerHour);

public sealed record AssistantCourtAvailabilityDto(
    int CourtId,
    string Name,
    IReadOnlyList<AssistantSlotDto> Slots);

public sealed record CourtAvailabilityToolResult(
    DateOnly Date,
    IReadOnlyList<AssistantCourtAvailabilityDto> Courts);

public sealed record AssistantToolError(string Error);