using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.System.Enums;

namespace ShuttleVNBackend.Application.DTOs.Bookings;

public record BookingListItemDto(
    Guid BookingId,
    string BookingCode,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    int CourtId,
    string CourtName,
    DateOnly Date,
    string StartTime,
    string EndTime,
    BookingStatus Status,
    decimal TotalCost,
    InvoiceStatus? PaymentStatus,
    DateTime CreatedAt);

public record BookingInvoiceDto(
    string InvoiceCode,
    InvoiceStatus Status,
    PaymentMethod? PaymentMethod,
    DateTime? PaidAt);

public record BookingStatusHistoryDto(
    BookingStatus? OldStatus,
    BookingStatus NewStatus,
    ActorType ActorType,
    string? ActorName,
    DateTime ChangedAt,
    string Reason);

public record BookingDetailDto(
    Guid BookingId,
    string BookingCode,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    int CourtId,
    string CourtName,
    DateOnly Date,
    string StartTime,
    string EndTime,
    BookingStatus Status,
    decimal TotalCost,
    InvoiceStatus? PaymentStatus,
    DateTime CreatedAt,
    BookingInvoiceDto? Invoice,
    IReadOnlyList<BookingStatusHistoryDto> StatusHistory);