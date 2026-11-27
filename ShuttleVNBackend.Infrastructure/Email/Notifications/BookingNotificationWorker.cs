using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShuttleVNBackend.Application.DTOs.Email;
using ShuttleVNBackend.Application.Interfaces;
using ShuttleVNBackend.Core.Entities.System;
using ShuttleVNBackend.Infrastructure.Persistence;

namespace ShuttleVNBackend.Infrastructure.Email.Notifications;

public sealed class BookingNotificationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<BookingNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), clock);
        do
        {
            try
            {
                await DeliverBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // Messages and inner exceptions can contain database details or provider credentials.
                logger.LogError(
                    "Booking notification batch failed ({ExceptionType}); pending messages will be retried. {StackTrace}",
                    exception.GetType().FullName, exception.StackTrace);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeliverBatchAsync(CancellationToken ct)
    {
        for (var batchIndex = 0; batchIndex < 20; batchIndex++)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ShuttleVnDbContext>();
            var now = clock.GetUtcNow().UtcDateTime;
            var lease = now.AddMinutes(2);
            var notification = await ClaimNotificationAsync(dbContext, now, lease, ct);
            if (notification is null) return;

            await DeliverNotificationAsync(dbContext, scope.ServiceProvider, notification, lease, ct);
        }
    }

    private static async Task<Notification?> ClaimNotificationAsync(
        ShuttleVnDbContext dbContext, DateTime now, DateTime lease, CancellationToken ct)
    {
        return await dbContext.ExecuteInTransactionAsync(async token =>
        {
            var notifications = await dbContext.Notifications
                .FromSqlInterpolated($"""
                                      SELECT * FROM "Notifications"
                                      WHERE "EventType" = 'BookingRescheduled' AND "Status" IN ('PENDING', 'SENDING')
                                        AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {now})
                                        AND ("LockedUntil" IS NULL OR "LockedUntil" <= {now})
                                      ORDER BY "CreatedAt", "NotificationId" FOR UPDATE SKIP LOCKED LIMIT 1
                                      """)
                .ToListAsync(token);
            var notification = notifications.FirstOrDefault();
            if (notification is null) return null;

            notification.Status = "SENDING";
            notification.LockedUntil = lease;
            notification.AttemptCount++;
            await dbContext.SaveSystemChangesAsync(token);
            return notification;
        }, ct);
    }

    private async Task DeliverNotificationAsync(
        ShuttleVnDbContext dbContext, IServiceProvider serviceProvider,
        Notification notification, DateTime lease, CancellationToken ct)
    {
        string? error = null;
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(30));
            using var payload = JsonDocument.Parse(notification.Payload);
            var root = payload.RootElement;
            var body = $"Booking {root.GetProperty("BookingCode").GetString()} was rescheduled.\n" +
                       $"Court: {root.GetProperty("CourtId")}\nDate: {root.GetProperty("Date")}\n" +
                       $"Time (Vietnam): {root.GetProperty("StartTime")}–{root.GetProperty("EndTime")}\n" +
                       $"Total: {root.GetProperty("TotalCost")} VND.";
            var emailService = serviceProvider.GetRequiredService<IEmailService>();
            await emailService.SendAsync(
                new EmailMessage(notification.RecipientEmail, "ShuttleVN booking rescheduled", body),
                deadline.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            error = "Delivery failed; retry scheduled.";
            logger.LogWarning("Booking notification {NotificationId} delivery failed on attempt {AttemptCount}.",
                notification.NotificationId, notification.AttemptCount);
        }

        var sentAt = clock.GetUtcNow().UtcDateTime;
        var nextAttempt = sentAt.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(6, notification.AttemptCount))));
        // A lease permits crash recovery. Delivery is at-least-once if provider accepts mail before a crash.
        await dbContext.Notifications
            .Where(notificationRow => notificationRow.NotificationId == notification.NotificationId
                                      && notificationRow.LockedUntil == lease)
            .ExecuteUpdateAsync(update => update
                .SetProperty(notificationRow => notificationRow.Status, error == null ? "SENT" : "PENDING")
                .SetProperty(notificationRow => notificationRow.SentAt, error == null ? sentAt : null)
                .SetProperty(notificationRow => notificationRow.NextAttemptAt, error == null ? null : nextAttempt)
                .SetProperty(notificationRow => notificationRow.LockedUntil, (DateTime?)null)
                .SetProperty(notificationRow => notificationRow.LastError, error), ct);
    }
}