using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.System.Enums;

namespace ShuttleVNBackend.Application.UseCases.Bookings.Services;

public static class InvoiceRules
{
    public static Invoice? ValidInvoice(Booking booking)
    {
        return booking.Invoices.SingleOrDefault(invoice =>
            invoice.Status is InvoiceStatus.Unpaid or InvoiceStatus.Paid);
    }

    public static string State(Booking booking)
    {
        var json = JsonSerializer.Serialize(new
        {
            booking.BookingId,
            booking.CourtId,
            booking.Date,
            booking.StartTime,
            booking.EndTime,
            booking.Status,
            TotalCost = CanonicalMoney(booking.TotalCost),
            UpdatedAt = booking.UpdatedAt.Ticks / 10,
            Invoices = booking.Invoices
                .OrderBy(invoice => invoice.InvoiceId)
                .Select(invoice => new
                {
                    invoice.InvoiceId,
                    invoice.Status,
                    TotalCost = CanonicalMoney(invoice.TotalCost),
                    PaidAmount = CanonicalMoney(invoice.PaidAmount),
                    RefundAmount = CanonicalMoney(invoice.RefundAmount),
                    PaidAt = invoice.PaidAt?.Ticks / 10,
                    invoice.PaymentMethod,
                    CancelledAt = invoice.CancelledAt?.Ticks / 10,
                    RefundedAt = invoice.RefundedAt?.Ticks / 10
                })
        });

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }

    private static string? CanonicalMoney(decimal? amount)
    {
        return amount?.ToString("G29", CultureInfo.InvariantCulture);
    }

    public static void CheckState(Booking booking, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected) || State(booking) != expected)
            throw new ConflictException(
                "Booking or invoice changed; reload before retrying. Do not repeat an external money transfer.");
    }

    public static void ValidateReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            throw new ValidationException("A reason containing 1–1000 characters is required.",
                new Dictionary<string, string[]>
                {
                    ["Reason"] = ["A non-empty reason up to 1000 characters is required."]
                });
    }

    public static void ValidatePayment(decimal amount, decimal total, PaymentMethod method, DateTime at,
        DateTime now)
    {
        if (amount != total || amount <= 0 || decimal.Truncate(amount) != amount)
            throw new ValidationException("Payment or refund amount must equal the full invoice total in whole VND.");

        if (!Enum.IsDefined(method) || at.Kind != DateTimeKind.Utc || at == default || at > now)
            throw new ValidationException(
                "A valid method and actual UTC payment/refund time no later than server time are required.");
    }

    public static void Cancel(Invoice invoice, string reason, ActorType actor, Guid actorId,
        RefundPaymentDto? refund, DateTime now)
    {
        ValidateReason(reason);
        if (invoice.Status == InvoiceStatus.Paid)
        {
            if (actor != ActorType.Employee)
                throw new ConflictException("Paid bookings must be handled by an employee.");

            if (refund is null)
                throw new ValidationException("Confirm the full external refund before cancelling a paid invoice.");

            ValidateReason(refund.Reason);
            ValidatePayment(refund.Amount, invoice.TotalCost, refund.Method, refund.RefundedAt, now);
            if (refund.RefundedAt < invoice.PaidAt)
                throw new ValidationException("Refund time must not precede payment time.");

            invoice.RefundedByEmployee = actorId;
            invoice.RefundedAt = refund.RefundedAt;
            invoice.RefundMethod = refund.Method;
            invoice.RefundAmount = refund.Amount;
            invoice.RefundReason = refund.Reason.Trim();
            invoice.Status = InvoiceStatus.Refunded;
        }
        else
        {
            if (invoice.Status != InvoiceStatus.Unpaid || refund is not null)
                throw new ConflictException("Only a valid paid invoice can receive a refund.");

            invoice.Status = InvoiceStatus.Cancelled;
        }

        invoice.CancelledAt = now;
        invoice.CancellationActorType = actor;
        invoice.CancelledByEmployee = actor == ActorType.Employee ? actorId : null;
        invoice.CancelledByCustomer = actor == ActorType.Customer ? actorId : null;
        invoice.CancellationReason = reason.Trim();
    }
}