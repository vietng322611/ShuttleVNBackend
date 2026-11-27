using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Application.DTOs.Bookings;

public sealed record RefundPaymentDto(
    decimal Amount,
    PaymentMethod Method,
    DateTime RefundedAt,
    string Reason);