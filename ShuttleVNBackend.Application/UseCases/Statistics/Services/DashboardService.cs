using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Statistics;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.UseCases.Bookings.Helpers;

namespace ShuttleVNBackend.Application.UseCases.Statistics.Services;

public class DashboardService(
    StatisticsService statisticsService,
    IBookingRepository bookingRepository,
    TimeProvider clock)
{
    private const int PendingPreviewSize = 10;

    public async Task<DashboardSummaryDto> GetTodayAsync(CancellationToken ct = default)
    {
        var today = VietnamTime.TodayLocal(clock);

        var usage = await statisticsService.GetCourtUsageAsync(today, today, RevenueGroupBy.Day, ct);
        var revenue = await statisticsService.GetRevenueAsync(today, today, RevenueGroupBy.Day, ct);
        var pending = await bookingRepository.GetPendingPreviewAsync(PendingPreviewSize, ct);

        return new DashboardSummaryDto(
            today,
            revenue.TotalRevenue,
            revenue.TotalInvoices,
            usage.BookingCount, usage.BookedHours, usage.CompletedCount, usage.CompletedHours,
            pending.TotalCount,
            usage.Courts,
            pending.Items.Select(r => r.ToListItem()).ToList());
    }
}