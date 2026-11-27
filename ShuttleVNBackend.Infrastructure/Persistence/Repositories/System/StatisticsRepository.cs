using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.DTOs.Statistics;
using ShuttleVNBackend.Application.Interfaces.Repositories.System;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories.System;

public class StatisticsRepository(ShuttleVnDbContext dbContext) : IStatisticsRepository
{
    public async Task<IReadOnlyList<CourtNameRow>> GetCourtsAsync(CancellationToken ct = default)
    {
        return await dbContext.BadmintonCourts.AsNoTracking()
            .OrderBy(c => c.CourtId)
            .Select(c => new CourtNameRow(c.CourtId, c.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CourtUsageRow>> GetUsageRowsAsync(
        DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        return await dbContext.Bookings.AsNoTracking()
            .Where(b => b.Date >= from && b.Date <= to && b.Status != BookingStatus.Cancelled)
            .Select(b => new CourtUsageRow(b.CourtId, b.Date, b.StartTime, b.EndTime, b.Status))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PaidInvoiceRow>> GetPaidInvoicesAsync(
        DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default)
    {
        return await dbContext.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Paid
                        && i.PaidAt >= fromUtc && i.PaidAt < toUtcExclusive)
            .Select(i => new PaidInvoiceRow(i.PaidAt!.Value, i.TotalCost))
            .ToListAsync(ct);
    }
}