namespace ShuttleVNBackend.Core.Entities.System;

public class Audit
{
    public Guid Id { get; set; }
    public Guid? AccountId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityName { get; set; } = null!;
    public string EntityId { get; set; } = null!;
    public string OldValue { get; set; } = null!;
    public string NewValue { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
}