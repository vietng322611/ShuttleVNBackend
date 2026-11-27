namespace ShuttleVNBackend.Application.DTOs.Bookings;

public sealed record CancelBookingDto(
    string ExpectedState,
    string Reason,
    RefundPaymentDto? Refund = null
);