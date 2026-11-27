namespace ShuttleVNBackend.Application.DTOs.Bookings;

public sealed record PricePreviewDto(
    int CourtId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime
);

public sealed record PricePreviewResponse(
    decimal TotalCost,
    IReadOnlyList<BookingPriceSegmentDto> Segments,
    string? QuoteFingerprint = null
);