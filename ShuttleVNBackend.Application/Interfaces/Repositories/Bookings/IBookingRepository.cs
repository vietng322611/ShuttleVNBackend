using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;

namespace ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;

public interface IBookingRepository
{
    Task<int> CountUpcomingByCourtAsync(int courtId, DateOnly fromDate, CancellationToken ct = default);
    Task<Booking?> GetByIdForUpdateAsync(Guid bookingId, CancellationToken ct = default);
    Task<PagedResult<BookingRow>> GetPendingAsync(PageRequest page, CancellationToken ct = default);
    Task<BookingRow?> GetRowByIdAsync(Guid bookingId, CancellationToken ct = default);
    Task<BookingInvoiceDto?> GetValidInvoiceAsync(Guid bookingId, CancellationToken ct = default);
    Task<List<BookingStatusHistoryDto>> GetStatusHistoryAsync(Guid bookingId, CancellationToken ct = default);
    Task<bool> HasOverlapAsync(int courtId, DateOnly date, TimeOnly start, TimeOnly end,
        Guid excludeBookingId, CancellationToken ct = default);
    Task<PagedResult<BookingRow>> GetByCustomerAsync(Guid customerId, PageRequest page,
        CancellationToken ct = default);
    Task<PagedResult<BookingRow>> SearchAsync(BookingSearchCriteria criteria, PageRequest page,
        CancellationToken ct = default);
}