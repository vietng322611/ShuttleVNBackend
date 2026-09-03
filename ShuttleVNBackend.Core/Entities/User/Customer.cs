namespace ShuttleVNBackend.Core.Entities.User;

public class Customer
{
    public Guid CustomerId { get; set; }
    public Guid? AccountId { get; set; }
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string Email { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}