using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Application.DTOs.Bookings;

public record BookingRow(
    Guid BookingId,
    string BookingCode,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    int CourtId,
    string CourtName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    BookingStatus Status,
    decimal TotalCost,
    InvoiceStatus? PaymentStatus,
    DateTime CreatedAt);