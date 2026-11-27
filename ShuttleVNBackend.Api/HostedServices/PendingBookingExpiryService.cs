using ShuttleVNBackend.Application.UseCases.Bookings.Services;

namespace ShuttleVNBackend.Api.HostedServices;

public sealed class PendingBookingExpiryService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<PendingBookingExpiryService> logger) : BackgroundService
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var bookingRequests = scope.ServiceProvider.GetRequiredService<BookingRequestService>();
                await bookingRequests.SettleEndedBookingsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to process expired bookings; retrying in one minute.");
                await Task.Delay(TimeSpan.FromMinutes(1), clock, stoppingToken);
                continue;
            }

            var now = clock.GetUtcNow();
            var localNow = TimeZoneInfo.ConvertTime(now, VietnamTimeZone);
            var nextBoundary = new DateTimeOffset(
                localNow.Year, localNow.Month, localNow.Day, localNow.Hour,
                localNow.Minute < 31 ? 1 : 31, 0, localNow.Offset);
            if (nextBoundary <= localNow)
                nextBoundary = nextBoundary.AddMinutes(30);

            var delay = nextBoundary - localNow;
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, clock, stoppingToken);
        }
    }
}