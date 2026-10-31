using ShuttleVNBackend.Core.Attributes;
using ShuttleVNBackend.Core.Entities.Users.Enums;

namespace ShuttleVNBackend.Core.Entities.Users;

public class UserAccount
{
    public Guid AccountId { get; set; }
    public string LoginEmail { get; set; } = null!;
    [NoAudit]
    public string PasswordHash { get; set; } = "";
    public AccountType AccountType { get; set; }
    public AccountStatus Status { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual Customer? Customer { get; set; }
    public virtual Employee? Employee { get; set; }

    public string GetRole() =>
        AccountType == AccountType.Customer ? "Customer"
        : Employee?.IsAdmin == true ? "Admin" : "Employee";
}
