using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.System.Enums;

namespace ShuttleVNBackend.Application.DTOs.Bookings;

public sealed record InvoiceDetailDto(
    Guid InvoiceId,
    string InvoiceCode,
    Guid BookingId,
    string BookingCode,
    InvoiceStatus Status,
    decimal TotalCost,
    Guid? IssuedByEmployee,
    DateTime IssuedAt,
    Guid? PaidByEmployee,
    DateTime? PaidAt,
    decimal? PaidAmount,
    PaymentMethod? PaymentMethod,
    DateTime? CancelledAt,
    ActorType? CancellationActorType,
    Guid? CancelledByEmployee,
    Guid? CancelledByCustomer,
    string? CancellationReason,
    Guid? RefundedByEmployee,
    DateTime? RefundedAt,
    PaymentMethod? RefundMethod,
    decimal? RefundAmount,
    string? RefundReason,
    Guid? ReplacesInvoiceId,
    string Note,
    string ExpectedState)
{
    public static InvoiceDetailDto FromEntity(Invoice invoice, string state)
    {
        return new InvoiceDetailDto(
            invoice.InvoiceId,
            invoice.InvoiceCode,
            invoice.BookingId,
            invoice.Booking.BookingCode,
            invoice.Status,
            invoice.TotalCost,
            invoice.IssuedByEmployee,
            invoice.IssuedAt,
            invoice.PaidByEmployee,
            invoice.PaidAt,
            invoice.PaidAmount,
            invoice.PaymentMethod,
            invoice.CancelledAt,
            invoice.CancellationActorType,
            invoice.CancelledByEmployee,
            invoice.CancelledByCustomer,
            invoice.CancellationReason,
            invoice.RefundedByEmployee,
            invoice.RefundedAt,
            invoice.RefundMethod,
            invoice.RefundAmount,
            invoice.RefundReason,
            invoice.ReplacesInvoiceId,
            invoice.Note,
            state);
    }
}