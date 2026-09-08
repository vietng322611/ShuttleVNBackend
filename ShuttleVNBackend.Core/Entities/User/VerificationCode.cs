using ShuttleVNBackend.Core.Entities.User.Enums;

namespace ShuttleVNBackend.Core.Entities.User;

public class VerificationCode
{
    public string Email { get; set; } = null!;
    public CodeType Type { get; set; }
    public string CodeHash { get; set; } = null!;
    public int Attempt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime ExpiresAt { get; set; }
}