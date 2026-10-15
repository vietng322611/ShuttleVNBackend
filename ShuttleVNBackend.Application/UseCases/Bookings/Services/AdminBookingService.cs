using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.UseCases.Bookings.Helpers;

namespace ShuttleVNBackend.Application.UseCases.Bookings.Services;

public class AdminBookingService(
    IBookingRepository bookingRepository,
    BookingRequestService bookingRequestService)
{
    public async Task<PagedResult<BookingListItemDto>> SearchAsync(BookingSearchCriteria criteria, PageRequest page,
        CancellationToken ct = default)
    {
        if (criteria.FromDate is { } from && criteria.ToDate is { } to && from > to)
            throw new ValidationException(errors: new Dictionary<string, string[]>
                { ["FromDate"] = ["From date must not be after to date"] });

        var normalized = criteria with { Keyword = criteria.Keyword?.Trim() };
        var rows = await bookingRepository.SearchAsync(normalized, page, ct);
        return new PagedResult<BookingListItemDto>
        {
            Items = rows.Items.Select(r => r.ToListItem()).ToList(),
            PageNumber = rows.PageNumber,
            PageSize = rows.PageSize,
            TotalCount = rows.TotalCount
        };
    }

    public Task<BookingDetailDto> GetDetailAsync(Guid bookingId, CancellationToken ct = default)
    {
        return bookingRequestService.GetDetailAsync(bookingId, ct);
    }
}