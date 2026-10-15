using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Application.DTOs.Bookings;

public record ConfirmBookingDto(
    bool IsPaid,
    PaymentMethod? PaymentMethod,
    DateTime? PaidAt,
    string? Note);