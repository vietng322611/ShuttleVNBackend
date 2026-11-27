using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Statistics;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories.System;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Application.UseCases.Statistics.Services;

public class StatisticsService(
    IStatisticsRepository statisticsRepository,
    TimeProvider clock)
{
    private const int MaxRangeDays = 366;

    public async Task<CourtUsageSummaryDto> GetCourtUsageAsync(
        DateOnly? fromDate, DateOnly? toDate, RevenueGroupBy groupBy, CancellationToken ct = default)
    {
        var (from, to) = ResolveRange(fromDate, toDate);

        var courts = await statisticsRepository.GetCourtsAsync(ct);
        var rows = await statisticsRepository.GetUsageRowsAsync(from, to, ct);

        var rowsByPeriod = rows.ToLookup(r => PeriodStart(r.Date, groupBy));
        var periods = EnumeratePeriods(from, to, groupBy)
            .Select(start =>
            {
                var u = SumUsage(rowsByPeriod[start]);
                return new CourtUsagePeriodDto(
                    start, u.BookingCount, u.BookedHours, u.CompletedCount, u.CompletedHours);
            })
            .ToList();

        var total = SumUsage(rows);

        return new CourtUsageSummaryDto(
            from, to, groupBy,
            total.BookingCount, total.BookedHours, total.CompletedCount, total.CompletedHours,
            periods, BuildCourts(courts, rows));
    }

    public async Task<RevenueSummaryDto> GetRevenueAsync(
        DateOnly? fromDate, DateOnly? toDate, RevenueGroupBy groupBy, CancellationToken ct = default)
    {
        var (from, to) = ResolveRange(fromDate, toDate);

        var invoices = await statisticsRepository.GetPaidInvoicesAsync(
            VietnamTime.ToUtc(from, TimeOnly.MinValue),
            VietnamTime.ToUtc(to.AddDays(1), TimeOnly.MinValue), ct);

        var totals = invoices
            .GroupBy(i => PeriodStart(VietnamTime.ToLocalDate(i.PaidAt), groupBy))
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Revenue: g.Sum(i => i.TotalCost)));

        var periods = EnumeratePeriods(from, to, groupBy)
            .Select(start => totals.TryGetValue(start, out var t)
                ? new RevenuePeriodDto(start, t.Count, t.Revenue)
                : new RevenuePeriodDto(start, 0, 0m))
            .ToList();

        return new RevenueSummaryDto(from, to, groupBy,
            periods.Sum(p => p.InvoiceCount), periods.Sum(p => p.Revenue), periods);
    }

    private (DateOnly From, DateOnly To) ResolveRange(DateOnly? fromDate, DateOnly? toDate)
    {
        var today = VietnamTime.TodayLocal(clock);
        var from = fromDate ?? new DateOnly(today.Year, today.Month, 1);
        var to = toDate ?? today;

        if (from > to || to.DayNumber - from.DayNumber >= MaxRangeDays)
            throw new ValidationException(errors: new Dictionary<string, string[]>
            {
                ["DateRange"] = [$"fromDate must be <= toDate and the range at most {MaxRangeDays} days"]
            });

        return (from, to);
    }

    private static Usage SumUsage(IEnumerable<CourtUsageRow> rows)
    {
        var bookingCount = 0;
        var completedCount = 0;
        var bookedMinutes = 0d;
        var completedMinutes = 0d;

        foreach (var row in rows)
        {
            var minutes = (row.EndTime - row.StartTime).TotalMinutes;
            bookingCount++;
            bookedMinutes += minutes;

            if (row.Status != BookingStatus.Completed) continue;

            completedCount++;
            completedMinutes += minutes;
        }

        return new Usage(bookingCount, ToHours(bookedMinutes), completedCount, ToHours(completedMinutes));
    }

    private static List<CourtUsageCourtDto> BuildCourts(
        IReadOnlyList<CourtNameRow> courts, IReadOnlyList<CourtUsageRow> rows)
    {
        var rowsByCourt = rows.ToLookup(r => r.CourtId);

        return courts
            .Select(c => (Court: c, Totals: SumUsage(rowsByCourt[c.CourtId])))
            .Where(x => x.Court.Name is not null || x.Totals.BookingCount > 0)
            .Select(x => new CourtUsageCourtDto(
                x.Court.CourtId,
                x.Court.Name ?? $"Sân đã xóa #{x.Court.CourtId}",
                x.Totals.BookingCount,
                x.Totals.BookedHours,
                x.Totals.CompletedCount,
                x.Totals.CompletedHours))
            .ToList();
    }

    private static double ToHours(double minutes)
    {
        return Math.Round(minutes / 60, 2);
    }

    private static DateOnly PeriodStart(DateOnly date, RevenueGroupBy groupBy)
    {
        return groupBy == RevenueGroupBy.Month ? new DateOnly(date.Year, date.Month, 1) : date;
    }

    private static IEnumerable<DateOnly> EnumeratePeriods(DateOnly from, DateOnly to, RevenueGroupBy groupBy)
    {
        var last = PeriodStart(to, groupBy);
        for (var current = PeriodStart(from, groupBy);
             current <= last;
             current = groupBy == RevenueGroupBy.Month ? current.AddMonths(1) : current.AddDays(1))
            yield return current;
    }

    private readonly record struct Usage(
        int BookingCount,
        double BookedHours,
        int CompletedCount,
        double CompletedHours);
}