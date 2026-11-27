using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Courts;

namespace ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;

public interface IBookingRepository
{
    Task<int> CountUpcomingByCourtAsync(int courtId, DateOnly fromDate, CancellationToken ct = default);
    Task<List<Guid>> GetSettlementCandidateIdsAsync(DateTime localCutoff, CancellationToken ct = default);
    Task<Booking?> GetByIdForUpdateAsync(Guid bookingId, CancellationToken ct = default);
    Task<PagedResult<BookingRow>> GetPendingAsync(PageRequest page, CancellationToken ct = default);
    Task<PagedResult<BookingRow>> GetPendingPreviewAsync(int take, CancellationToken ct = default);
    Task<BookingRow?> GetRowByIdAsync(Guid bookingId, CancellationToken ct = default);
    Task<BookingInvoiceDto?> GetValidInvoiceAsync(Guid bookingId, CancellationToken ct = default);
    Task<List<BookingStatusHistoryDto>> GetStatusHistoryAsync(Guid bookingId, CancellationToken ct = default);

    Task<bool> HasOverlapAsync(int courtId, DateOnly date, TimeOnly start, TimeOnly end,
        Guid excludeBookingId, CancellationToken ct = default);

    Task<PagedResult<BookingRow>> GetByCustomerAsync(Guid customerId, PageRequest page,
        CancellationToken ct = default);

    Task<PagedResult<BookingRow>> SearchAsync(BookingSearchCriteria criteria, PageRequest page,
        CancellationToken ct = default);

    Task<PagedResult<BookingRow>> SearchByLookupAsync(QuickLookupQuery query, PageRequest page,
        CancellationToken ct = default);

    Task<Booking?> GetByIdAsync(Guid bookingId, CancellationToken ct = default);
    Task<Booking?> GetByCodeAsync(string bookingCode, string? phoneOrEmail, CancellationToken ct = default);
    Task<Booking?> GetByIdWithDetailsAsync(Guid bookingId, CancellationToken ct = default);
    Task<Booking?> GetFreshDetailsAsync(Guid bookingId, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);

    Task<PagedResult<Booking>> GetByCustomerWithDetailsAsync(Guid customerId, PageRequest page,
        CancellationToken ct = default);

    Task<CourtSchedule?> GetScheduleAsync(int courtId, int dayOfWeek, CancellationToken ct = default);
    Task<List<PricingRule>> GetPricingRulesAsync(int courtId, int dayOfWeek, CancellationToken ct = default);
}