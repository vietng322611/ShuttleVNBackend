using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.Courts;
using ShuttleVNBackend.Core.Entities.Users;

namespace ShuttleVNBackend.Core.Entities.Bookings;

public class Booking
{
    private BadmintonCourt? _court;

    private Customer? _customer;

    private ICollection<Invoice>? _invoices;

    private ICollection<BookingPriceSegment>? _priceSegments;
    public Guid BookingId { get; set; }
    public string BookingCode { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public int CourtId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public BookingStatus Status { get; set; }
    public decimal TotalCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Customer Customer
    {
        get => _customer ?? throw new InvalidOperationException("Uninitialized property: " + nameof(Customer));
        set => _customer = value;
    }

    public virtual BadmintonCourt Court
    {
        get => _court ?? throw new InvalidOperationException("Uninitialized property: " + nameof(Court));
        set => _court = value;
    }

    public virtual ICollection<BookingPriceSegment> PriceSegments
    {
        get => _priceSegments ??
               throw new InvalidOperationException("Uninitialized property: " + nameof(PriceSegments));
        set => _priceSegments = value;
    }

    public virtual ICollection<Invoice> Invoices
    {
        get => _invoices ?? throw new InvalidOperationException("Uninitialized property: " + nameof(Invoices));
        set => _invoices = value;
    }
}