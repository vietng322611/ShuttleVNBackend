using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Courts;
using ShuttleVNBackend.Core.Entities.Users;

namespace ShuttleVNBackend.Core.Tests.Entities;

// Navigation getters throw instead of returning null so a missing EF Include
// fails loudly, and set them once EF materialises the graph.
public class NavigationGuardTests
{
    [Fact]
    public void BookingNavigations_ThrowBeforeAssignment_AndReturnAssignedGraph()
    {
        var booking = new Booking();

        Assert.Throws<InvalidOperationException>(() => booking.Customer);
        Assert.Throws<InvalidOperationException>(() => booking.Court);
        Assert.Throws<InvalidOperationException>(() => booking.PriceSegments);
        Assert.Throws<InvalidOperationException>(() => booking.Invoices);

        var customer = new Customer { CustomerId = Guid.NewGuid() };
        var court = new BadmintonCourt { CourtId = 1 };
        var segments = new List<BookingPriceSegment> { new() { SegmentNo = 1 } };
        var invoices = new List<Invoice> { new() { InvoiceCode = "HD-20260101-0001" } };

        booking.Customer = customer;
        booking.Court = court;
        booking.PriceSegments = segments;
        booking.Invoices = invoices;

        Assert.Same(customer, booking.Customer);
        Assert.Same(court, booking.Court);
        Assert.Same(segments, booking.PriceSegments);
        Assert.Same(invoices, booking.Invoices);
    }

    [Fact]
    public void BadmintonCourtNavigations_ThrowBeforeAssignment_AndReturnAssignedGraph()
    {
        var court = new BadmintonCourt();

        Assert.Throws<InvalidOperationException>(() => court.CourtSchedules);
        Assert.Throws<InvalidOperationException>(() => court.PricingRules);

        var schedules = new List<CourtSchedule>();
        var rules = new List<PricingRule>();

        court.CourtSchedules = schedules;
        court.PricingRules = rules;

        Assert.Same(schedules, court.CourtSchedules);
        Assert.Same(rules, court.PricingRules);
    }

    [Fact]
    public void BookingChildNavigations_ThrowBeforeAssignment_AndReturnAssignedBooking()
    {
        var booking = new Booking { BookingId = Guid.NewGuid() };
        var segment = new BookingPriceSegment();
        var history = new BookingStatusHistory();

        Assert.Throws<InvalidOperationException>(() => segment.Booking);
        Assert.Throws<InvalidOperationException>(() => history.Booking);

        segment.Booking = booking;
        history.Booking = booking;

        Assert.Same(booking, segment.Booking);
        Assert.Same(booking, history.Booking);
    }
}
