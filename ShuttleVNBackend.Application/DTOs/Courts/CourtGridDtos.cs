namespace ShuttleVNBackend.Application.DTOs.Courts;

public record CourtSlotDto(
    int CourtId,
    DateOnly Date,
    string StartTime,
    string EndTime,
    string DisplayStatus,
    Guid? BookingId,
    decimal PricePerHour,
    bool IsMine);

public record CourtGridItemDto(CourtDto Court, IReadOnlyList<CourtSlotDto> Slots);

public record CourtGridResponseDto(DateOnly Date, IReadOnlyList<CourtGridItemDto> Courts);