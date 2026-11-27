namespace ShuttleVNBackend.Core.Entities.System;

public class Notification
{
    public Guid NotificationId { get; set; }
    public string EventKey { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public string RecipientEmail { get; set; } = null!;
    public Guid BookingId { get; set; }
    public string EventType { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}