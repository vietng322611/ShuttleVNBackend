namespace ShuttleVNBackend.Core.Entities.User;

public class Employee
{
    public Guid EmployeeId { get; set; }
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string Email { get; set; } = null!;
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}