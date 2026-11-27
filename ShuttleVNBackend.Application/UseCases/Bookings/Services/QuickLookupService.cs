using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.DTOs.Users;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.Interfaces.Repositories.Users;
using ShuttleVNBackend.Application.UseCases.Bookings.Helpers;

namespace ShuttleVNBackend.Application.UseCases.Bookings.Services;

public class QuickLookupService(
    IBookingRepository bookingRepository,
    ICustomerRepository customerRepository)
{
    public async Task<QuickLookupResultDto> LookupAsync(
        string? keyword, PageRequest page, CancellationToken ct = default)
    {
        var query = QuickLookupQueryParser.Parse(keyword);
        var rows = await bookingRepository.SearchByLookupAsync(query, page, ct);
        var customers = await FindCustomersAsync(query, rows, ct);

        var bookings = new PagedResult<BookingListItemDto>
        {
            Items = rows.Items.Select(r => r.ToListItem()).ToList(),
            PageNumber = rows.PageNumber,
            PageSize = rows.PageSize,
            TotalCount = rows.TotalCount
        };

        return new QuickLookupResultDto(query.MatchType, customers, bookings);
    }

    private Task<IReadOnlyList<CustomerListItemDto>> FindCustomersAsync(
        QuickLookupQuery query, PagedResult<BookingRow> rows, CancellationToken ct)
    {
        return query.MatchType switch
        {
            QuickLookupMatchType.Email => FindByEmailAsync(query.Value, ct),
            QuickLookupMatchType.Phone => customerRepository.GetListItemsByPhoneAsync(query.Value, ct),
            _ => FindByBookingAsync(rows, ct)
        };
    }

    private async Task<IReadOnlyList<CustomerListItemDto>> FindByEmailAsync(string email, CancellationToken ct)
    {
        var customer = await customerRepository.GetListItemByEmailAsync(email, ct);
        if (customer is null) return [];

        return [customer];
    }

    private async Task<IReadOnlyList<CustomerListItemDto>> FindByBookingAsync(
        PagedResult<BookingRow> rows, CancellationToken ct)
    {
        if (rows.Items.Count == 0) return [];

        var customer = await customerRepository.GetListItemByIdAsync(rows.Items[0].CustomerId, ct);
        if (customer is null) return [];

        return [customer];
    }
}