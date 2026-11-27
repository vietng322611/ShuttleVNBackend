namespace ShuttleVNBackend.Application.DTOs.Bookings;

public sealed record RescheduleBookingDto(
    int CourtId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal? TotalCost,
    string? QuoteFingerprint,
    string ExpectedState,
    string Reason,
    RefundPaymentDto? Refund = null)
{
    public PricePreviewDto ToPricePreviewDto()
    {
        return new PricePreviewDto(CourtId, Date, StartTime, EndTime);
    }

    public CreateBookingDto ToCreateBookingDto()
    {
        return new CreateBookingDto(CourtId, Date, StartTime, EndTime, TotalCost, QuoteFingerprint);
    }
}