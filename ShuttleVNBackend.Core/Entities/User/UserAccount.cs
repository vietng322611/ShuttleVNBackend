using ShuttleVNBackend.Core.Entities.User.Enums;

namespace ShuttleVNBackend.Core.Entities.User;

public class UserAccount
{
    public Guid AccountId { get; set; }
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = "";
    public AccountStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}