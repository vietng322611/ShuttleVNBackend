using ShuttleVNBackend.Core.Attributes;

namespace ShuttleVNBackend.Core.Entities.System;

[NoAudit]
public class IdempotencyRequest
{
    public Guid Id { get; set; }
    public string ActorScope { get; set; } = null!;
    public string RequestKey { get; set; } = null!;
    public string Operation { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public Guid? BookingId { get; set; }
    public Guid? InvoiceId { get; set; }
    public int ResponseStatus { get; set; }
    public string ResponseBody { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}