using ShuttleVNBackend.Core.Attributes;
using ShuttleVNBackend.Core.Entities.System.Enums;
using ShuttleVNBackend.Core.Entities.Users;

namespace ShuttleVNBackend.Core.Entities.System;

[NoAudit]
public class Audit
{
    public long Id { get; set; }
    public ActorType ActorType { get; set; }
    public Guid? EmployeeId { get; set; }
    public Guid? CustomerId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Employee? Employee { get; set; }
    public virtual Customer? Customer { get; set; }
}