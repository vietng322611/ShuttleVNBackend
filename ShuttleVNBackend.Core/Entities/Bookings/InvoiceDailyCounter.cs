using ShuttleVNBackend.Core.Attributes;

namespace ShuttleVNBackend.Core.Entities.Bookings;

[NoAudit]
public class InvoiceDailyCounter
{
    public DateOnly IssueDate { get; set; }
    public long LastNumber { get; set; }
}