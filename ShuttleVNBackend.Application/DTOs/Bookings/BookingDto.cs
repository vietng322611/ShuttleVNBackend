using ShuttleVNBackend.Application.UseCases.Bookings.Services;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Application.DTOs.Bookings;

public sealed record BookingDto(
    Guid BookingId,
    string BookingCode,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    int CourtId,
    string CourtName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    BookingStatus Status,
    decimal TotalCost,
    DateTime CreatedAt,
    IReadOnlyList<BookingPriceSegmentDto> PriceSegments,
    InvoiceDto? Invoice,
    string ExpectedState)
{
    public static BookingDto FromEntity(Booking booking)
    {
        var latestInvoice = booking.Invoices
            .OrderByDescending(i => i.IssuedAt)
            .FirstOrDefault();

        return new BookingDto(
            booking.BookingId,
            booking.BookingCode,
            booking.CustomerId,
            booking.Customer.FullName,
            booking.Customer.Email,
            booking.Customer.Phone,
            booking.CourtId,
            booking.Court.Name ?? string.Empty,
            booking.Date,
            booking.StartTime,
            booking.EndTime,
            booking.Status,
            booking.TotalCost,
            booking.CreatedAt,
            booking.PriceSegments
                .OrderBy(s => s.SegmentNo)
                .Select(BookingPriceSegmentDto.FromEntity)
                .ToList(),
            latestInvoice is null ? null : InvoiceDto.FromEntity(latestInvoice),
            InvoiceRules.State(booking));
    }
}

public sealed record BookingPriceSegmentDto(
    int SegmentNo,
    TimeOnly PriceStartTime,
    TimeOnly SegmentStartTime,
    TimeOnly SegmentEndTime,
    decimal PricePerHour
)
{
    public BookingPriceSegment ToEntity(Guid bookingId)
    {
        return new BookingPriceSegment
        {
            BookingId = bookingId,
            SegmentNo = SegmentNo,
            PriceStartTime = PriceStartTime,
            SegmentStartTime = SegmentStartTime,
            SegmentEndTime = SegmentEndTime,
            PricePerHour = PricePerHour
        };
    }

    public static BookingPriceSegmentDto FromEntity(BookingPriceSegment segment)
    {
        return new BookingPriceSegmentDto(
            segment.SegmentNo,
            segment.PriceStartTime,
            segment.SegmentStartTime,
            segment.SegmentEndTime,
            segment.PricePerHour);
    }
}