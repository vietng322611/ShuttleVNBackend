namespace ShuttleVNBackend.Application.DTOs.Bookings;

public sealed record ReschedulePreviewDto(
    string ExpectedState,
    PricePreviewResponse Quote,
    bool RequiresRefund,
    Guid? InvoiceId);