using ShuttleVNBackend.Core.Entities.Booking.Enums;

namespace ShuttleVNBackend.Core.Entities.Booking;

public class Invoice
{
    public Guid InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = null!;
    public Guid BookingId { get; set; }
    public decimal TotalCost { get; set; }
    public InvoiceStatus Status { get; set; }
    public Guid IssuedBy { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public string Note { get; set; } = string.Empty;
}