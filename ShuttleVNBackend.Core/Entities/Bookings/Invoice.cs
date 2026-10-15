using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.System.Enums;
using ShuttleVNBackend.Core.Entities.Users;

namespace ShuttleVNBackend.Core.Entities.Bookings;

public class Invoice
{
    public Guid InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = null!;
    public Guid BookingId { get; set; }
    public decimal TotalCost { get; set; }
    public InvoiceStatus Status { get; set; }
    public Guid IssuedByEmployee { get; set; }
    public DateTime IssuedAt { get; set; }
    public Guid? PaidByEmployee { get; set; }
    public DateTime? PaidAt { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public Guid? CancelledByEmployee { get; set; }
    public DateTime? CancelledAt { get; set; }
    public ActorType? CancellationActorType { get; set; }
    public Guid? CancelledByCustomer { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? RefundedByEmployee { get; set; }
    public DateTime? RefundedAt { get; set; }
    public PaymentMethod? RefundMethod { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? RefundReason { get; set; }
    public Guid? ReplacesInvoiceId { get; set; }
    public string Note { get; set; } = string.Empty;

    public virtual Booking Booking { get; set; } = null!;
    public virtual Employee IssueEmployee { get; set; } = null!;
    public virtual Employee? PaidEmployee { get; set; }
}