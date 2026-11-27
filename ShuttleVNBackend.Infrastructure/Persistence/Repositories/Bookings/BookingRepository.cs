using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.UseCases.Users.Helpers;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.Courts;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories.Bookings;

public class BookingRepository(ShuttleVnDbContext dbContext) : IBookingRepository
{
    public async Task<int> CountUpcomingByCourtAsync(int courtId, DateOnly fromDate, CancellationToken ct = default)
    {
        return await dbContext.Bookings.CountAsync(b =>
            b.CourtId == courtId && b.Date >= fromDate
                                 && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed), ct);
    }

    public Task<List<Guid>> GetSettlementCandidateIdsAsync(DateTime localCutoff, CancellationToken ct = default)
    {
        var date = DateOnly.FromDateTime(localCutoff);
        var time = TimeOnly.FromDateTime(localCutoff);
        return dbContext.Bookings
            .AsNoTracking()
            .Where(b => (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                        && (b.Date < date || (b.Date == date && b.EndTime <= time)))
            .Select(b => b.BookingId)
            .ToListAsync(ct);
    }

    public async Task<Booking?> GetByIdForUpdateAsync(Guid bookingId, CancellationToken ct = default)
    {
        var rows = await dbContext.Bookings
            .FromSqlInterpolated($"""SELECT * FROM "Bookings" WHERE "BookingId" = {bookingId} FOR UPDATE""")
            .ToListAsync(ct);
        return rows.FirstOrDefault();
    }

    public async Task<PagedResult<BookingRow>> GetPendingAsync(PageRequest page, CancellationToken ct = default)
    {
        var query = dbContext.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Pending);

        var totalCount = await query.CountAsync(ct);
        var items = await ProjectRows(query
                .OrderBy(b => b.CreatedAt).ThenBy(b => b.BookingId)
                .Skip((page.PageNumber - 1) * page.PageSize)
                .Take(page.PageSize))
            .ToListAsync(ct);

        return new PagedResult<BookingRow>
        {
            Items = items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PagedResult<BookingRow>> GetPendingPreviewAsync(int take, CancellationToken ct = default)
    {
        var query = dbContext.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Pending);

        var totalCount = await query.CountAsync(ct);
        var items = await ProjectRows(query
                .OrderBy(b => b.Date).ThenBy(b => b.StartTime)
                .ThenBy(b => b.CreatedAt).ThenBy(b => b.BookingId)
                .Take(take))
            .ToListAsync(ct);

        return new PagedResult<BookingRow>
        {
            Items = items,
            PageNumber = 1,
            PageSize = take,
            TotalCount = totalCount
        };
    }

    public async Task<BookingRow?> GetRowByIdAsync(Guid bookingId, CancellationToken ct = default)
    {
        return await ProjectRows(dbContext.Bookings.AsNoTracking().Where(b => b.BookingId == bookingId))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BookingInvoiceDto?> GetValidInvoiceAsync(Guid bookingId, CancellationToken ct = default)
    {
        return await dbContext.Invoices.AsNoTracking()
            .Where(i => i.BookingId == bookingId
                        && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Paid))
            .Select(i => new BookingInvoiceDto(i.InvoiceCode, i.Status, i.PaymentMethod, i.PaidAt))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<BookingStatusHistoryDto>> GetStatusHistoryAsync(Guid bookingId,
        CancellationToken ct = default)
    {
        return await dbContext.BookingStatusHistories.AsNoTracking()
            .Where(h => h.BookingId == bookingId)
            .OrderBy(h => h.ChangedAt).ThenBy(h => h.Id)
            .Select(h => new BookingStatusHistoryDto(
                h.OldStatus,
                h.NewStatus,
                h.ActorType,
                h.Employee != null ? h.Employee.FullName
                : h.Customer != null ? h.Customer.FullName
                : null,
                h.ChangedAt,
                h.Reason))
            .ToListAsync(ct);
    }

    public async Task<bool> HasOverlapAsync(int courtId, DateOnly date, TimeOnly start, TimeOnly end,
        Guid excludeBookingId, CancellationToken ct = default)
    {
        return await dbContext.Bookings.AnyAsync(b =>
            b.CourtId == courtId && b.Date == date && b.BookingId != excludeBookingId
            && b.Status != BookingStatus.Cancelled
            && b.StartTime < end && b.EndTime > start, ct);
    }

    public async Task<PagedResult<BookingRow>> GetByCustomerAsync(Guid customerId, PageRequest page,
        CancellationToken ct = default)
    {
        var query = dbContext.Bookings.AsNoTracking().Where(b => b.CustomerId == customerId);

        var totalCount = await query.CountAsync(ct);
        var items = await ProjectRows(query
                .OrderByDescending(b => b.Date)
                .ThenByDescending(b => b.StartTime)
                .ThenByDescending(b => b.CreatedAt)
                .ThenBy(b => b.BookingId)
                .Skip((page.PageNumber - 1) * page.PageSize)
                .Take(page.PageSize))
            .ToListAsync(ct);

        return new PagedResult<BookingRow>
        {
            Items = items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PagedResult<BookingRow>> SearchAsync(BookingSearchCriteria criteria, PageRequest page,
        CancellationToken ct = default)
    {
        var query = dbContext.Bookings.AsNoTracking().AsQueryable();

        if (criteria.FromDate is { } from) query = query.Where(b => b.Date >= from);
        if (criteria.ToDate is { } to) query = query.Where(b => b.Date <= to);
        if (criteria.CourtId is { } courtId) query = query.Where(b => b.CourtId == courtId);
        if (criteria.Status is { } status) query = query.Where(b => b.Status == status);
        if (criteria.CustomerId is { } customerId) query = query.Where(b => b.CustomerId == customerId);

        var term = criteria.Keyword?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = ToLikePattern(term);
            var phonePattern = ToLikePattern(CustomerProfileRules.NormalizePhone(term));
            query = query.Where(b =>
                EF.Functions.ILike(b.BookingCode, pattern)
                || EF.Functions.ILike(b.Customer.FullName, pattern)
                || EF.Functions.ILike(b.Customer.Email, pattern)
                || EF.Functions.ILike(b.Customer.Phone, phonePattern));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await ProjectRows(query
                .OrderByDescending(b => b.Date)
                .ThenByDescending(b => b.StartTime)
                .ThenBy(b => b.BookingId)
                .Skip((page.PageNumber - 1) * page.PageSize)
                .Take(page.PageSize))
            .ToListAsync(ct);

        return new PagedResult<BookingRow>
        {
            Items = items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<PagedResult<BookingRow>> SearchByLookupAsync(
        QuickLookupQuery query, PageRequest page, CancellationToken ct = default)
    {
        var value = query.Value;
        var bookings = dbContext.Bookings.AsNoTracking();
        var filtered = query.MatchType switch
        {
            QuickLookupMatchType.BookingCode => bookings.Where(b => b.BookingCode == value),
            QuickLookupMatchType.Email => bookings.Where(b => b.Customer.Email.ToLower().Trim() == value),
            _ => bookings.Where(b => b.Customer.Phone.Replace(" ", "").Replace("-", "") == value)
        };

        var totalCount = await filtered.CountAsync(ct);
        var items = await ProjectRows(filtered
                .OrderByDescending(b => b.Date)
                .ThenByDescending(b => b.StartTime)
                .ThenBy(b => b.BookingId)
                .Skip((page.PageNumber - 1) * page.PageSize)
                .Take(page.PageSize))
            .ToListAsync(ct);

        return new PagedResult<BookingRow>
        {
            Items = items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<Booking?> GetByIdAsync(Guid bookingId, CancellationToken ct = default)
    {
        return await dbContext.Bookings
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct);
    }

    public async Task<Booking?> GetByCodeAsync(string bookingCode, string? phoneOrEmail, CancellationToken ct = default)
    {
        var query = dbContext.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Court)
            .Include(b => b.PriceSegments)
            .Include(b => b.Invoices)
            .AsSplitQuery()
            .AsNoTracking()
            .Where(b => b.BookingCode == bookingCode);

        if (!string.IsNullOrWhiteSpace(phoneOrEmail))
        {
            var trimmed = phoneOrEmail.Trim();
            query = query.Where(b =>
                b.Customer.Phone == trimmed ||
                b.Customer.Email.Equals(trimmed, StringComparison.CurrentCultureIgnoreCase));
        }

        return await query.FirstOrDefaultAsync(ct);
    }

    public async Task<Booking?> GetByIdWithDetailsAsync(Guid bookingId, CancellationToken ct = default)
    {
        return await dbContext.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Court)
            .Include(b => b.PriceSegments)
            .Include(b => b.Invoices)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, ct);
    }

    public async Task<Booking?> GetFreshDetailsAsync(Guid bookingId, CancellationToken ct = default)
    {
        var entries = dbContext.ChangeTracker.Entries()
            .Where(entry => (entry.Entity is Booking booking && booking.BookingId == bookingId) ||
                            (entry.Entity is Invoice invoice && invoice.BookingId == bookingId) ||
                            (entry.Entity is BookingPriceSegment priceSegment && priceSegment.BookingId == bookingId))
            .ToList();

        if (entries.Any(entry => entry.State is not EntityState.Unchanged))
            throw new InvalidOperationException("Cannot refresh a booking with unsaved changes.");

        foreach (var entry in entries) entry.State = EntityState.Detached;

        return await GetByIdWithDetailsAsync(bookingId, ct);
    }

    public async Task<bool> CodeExistsAsync(string code, CancellationToken ct = default)
    {
        return await dbContext.Bookings.AnyAsync(b => b.BookingCode == code, ct);
    }

    public async Task<PagedResult<Booking>> GetByCustomerWithDetailsAsync(
        Guid customerId, PageRequest page, CancellationToken ct = default)
    {
        var query = dbContext.Bookings
            .Include(b => b.Court)
            .Include(b => b.PriceSegments)
            .Include(b => b.Invoices)
            .AsSplitQuery()
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.Date)
            .ThenByDescending(b => b.StartTime)
            .ThenByDescending(b => b.CreatedAt)
            .ThenBy(b => b.BookingId);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Booking>
        {
            Items = items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = total
        };
    }

    public async Task<CourtSchedule?> GetScheduleAsync(int courtId, int dayOfWeek, CancellationToken ct = default)
    {
        return await dbContext.CourtSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CourtId == courtId && s.DayOfWeek == dayOfWeek, ct);
    }

    public async Task<List<PricingRule>> GetPricingRulesAsync(int courtId, int dayOfWeek,
        CancellationToken ct = default)
    {
        return await dbContext.PricingRules
            .AsNoTracking()
            .Where(r => r.CourtId == courtId && r.DayOfWeek == dayOfWeek)
            .OrderBy(r => r.StartTime)
            .ToListAsync(ct);
    }

    private IQueryable<BookingRow> ProjectRows(IQueryable<Booking> source)
    {
        return source.Select(b => new BookingRow(
            b.BookingId,
            b.BookingCode,
            b.CustomerId,
            b.Customer.FullName,
            b.Customer.Phone,
            b.Customer.Email,
            b.CourtId,
            b.Court.Name ?? "DELETED", // to verify why this is nullable
            b.Date,
            b.StartTime,
            b.EndTime,
            b.Status,
            b.TotalCost,
            dbContext.Invoices
                .Where(i => i.BookingId == b.BookingId
                            && (i.Status == InvoiceStatus.Unpaid || i.Status == InvoiceStatus.Paid))
                .Select(i => (InvoiceStatus?)i.Status)
                .FirstOrDefault(),
            b.CreatedAt));
    }

    private static string ToLikePattern(string term)
    {
        return $"%{term.Replace("\\", @"\\").Replace("%", "\\%").Replace("_", "\\_")}%";
    }
}