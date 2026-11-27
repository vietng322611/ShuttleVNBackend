using ShuttleVNBackend.Application.DTOs.Bookings;

namespace ShuttleVNBackend.Application.DTOs.Statistics;

public record DashboardSummaryDto(
    DateOnly Date,
    decimal Revenue,
    int PaidInvoiceCount,
    int BookingCount,
    double BookedHours,
    int CompletedCount,
    double CompletedHours,
    int PendingCount,
    IReadOnlyList<CourtUsageCourtDto> Courts,
    IReadOnlyList<BookingListItemDto> PendingBookings);