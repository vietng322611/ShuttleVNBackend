using ShuttleVNBackend.Application.DTOs.Statistics;

namespace ShuttleVNBackend.Application.Interfaces.Repositories.System;

public interface IStatisticsRepository
{
    Task<IReadOnlyList<CourtNameRow>> GetCourtsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CourtUsageRow>> GetUsageRowsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<IReadOnlyList<PaidInvoiceRow>> GetPaidInvoicesAsync(
        DateTime fromUtc, DateTime toUtcExclusive, CancellationToken ct = default);
}