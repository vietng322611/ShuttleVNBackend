namespace ShuttleVNBackend.Application.DTOs.Statistics;

public record CourtUsagePeriodDto(
    DateOnly PeriodStart,
    int BookingCount,
    double BookedHours,
    int CompletedCount,
    double CompletedHours);

public record CourtUsageCourtDto(
    int CourtId,
    string CourtName,
    int BookingCount,
    double BookedHours,
    int CompletedCount,
    double CompletedHours);

public record CourtUsageSummaryDto(
    DateOnly FromDate,
    DateOnly ToDate,
    RevenueGroupBy GroupBy,
    int BookingCount,
    double BookedHours,
    int CompletedCount,
    double CompletedHours,
    IReadOnlyList<CourtUsagePeriodDto> Periods,
    IReadOnlyList<CourtUsageCourtDto> Courts);