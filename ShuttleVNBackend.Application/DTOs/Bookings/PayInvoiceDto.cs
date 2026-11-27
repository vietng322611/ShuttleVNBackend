using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Application.DTOs.Bookings;

public sealed record PayInvoiceDto(
    string ExpectedState,
    decimal Amount,
    PaymentMethod PaymentMethod,
    DateTime PaidAt);