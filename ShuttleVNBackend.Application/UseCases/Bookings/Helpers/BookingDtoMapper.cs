using System.Globalization;
using ShuttleVNBackend.Application.DTOs.Bookings;

namespace ShuttleVNBackend.Application.UseCases.Bookings.Helpers;

public static class BookingDtoMapper
{
    private const string TimeFormat = "HH:mm";

    public static BookingListItemDto ToListItem(this BookingRow r)
    {
        return new BookingListItemDto(
            r.BookingId, r.BookingCode, r.CustomerId, r.CustomerName, r.CustomerPhone,
            r.CourtId, r.CourtName, r.Date,
            r.StartTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
            r.EndTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
            r.Status, r.TotalCost, r.PaymentStatus, r.CreatedAt);
    }

    public static BookingDetailDto ToDetail(
        this BookingRow r,
        BookingInvoiceDto? invoice,
        IReadOnlyList<BookingStatusHistoryDto> history)
    {
        return new BookingDetailDto(
            r.BookingId, r.BookingCode, r.CustomerId, r.CustomerName, r.CustomerPhone, r.CustomerEmail,
            r.CourtId, r.CourtName, r.Date,
            r.StartTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
            r.EndTime.ToString(TimeFormat, CultureInfo.InvariantCulture),
            r.Status, r.TotalCost, r.PaymentStatus, r.CreatedAt,
            invoice, history);
    }
}