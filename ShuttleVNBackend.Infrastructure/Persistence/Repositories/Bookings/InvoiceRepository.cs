using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories.Bookings;

public sealed class InvoiceRepository(ShuttleVnDbContext dbContext) : IInvoiceRepository
{
    public async Task<PagedResult<Invoice>> SearchAsync(PageRequest page, InvoiceStatus? status, string? code,
        CancellationToken ct = default)
    {
        var query = dbContext.Invoices
            .AsNoTrackingWithIdentityResolution()
            .Include(i => i.Booking)
            .ThenInclude(b => b.Invoices)
            .AsQueryable();

        if (status is not null)
            query = query.Where(i => i.Status == status);

        if (!string.IsNullOrWhiteSpace(code))
        {
            var term = code.Trim().ToUpperInvariant();
            query = query.Where(i => i.InvoiceCode.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.IssuedAt)
            .ThenBy(i => i.InvoiceId)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Invoice>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize
        };
    }

    public Task<Invoice?> GetByIdAsync(Guid invoiceId, CancellationToken ct = default)
    {
        return dbContext.Invoices
            .AsNoTrackingWithIdentityResolution()
            .Include(i => i.Booking)
            .ThenInclude(b => b.Invoices)
            .SingleOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);
    }

    public Task<List<Invoice>> GetHistoryAsync(Guid bookingId, CancellationToken ct = default)
    {
        return dbContext.Invoices
            .AsNoTrackingWithIdentityResolution()
            .Include(i => i.Booking)
            .ThenInclude(b => b.Invoices)
            .Where(i => i.BookingId == bookingId)
            .OrderBy(i => i.IssuedAt)
            .ThenBy(i => i.InvoiceId)
            .ToListAsync(ct);
    }
}