using ShuttleVNBackend.Core.Entities.Users;
using ShuttleVNBackend.Core.Entities.Users.Enums;

namespace ShuttleVNBackend.Application.Interfaces.Repositories.Users;

public interface IVerificationCodeRepository
{
    Task<VerificationCode?> GetActiveAsync(string email, CodeType type, CancellationToken ct = default);
    Task<VerificationCode?> GetAsync(string email, CodeType type, CancellationToken ct = default);
    Task<bool> TryConsumeAsync(string email, CodeType type, string codeHash, DateTime nowUtc,
        CancellationToken ct = default);
    Task DeleteExistingAsync(string email, CodeType type);
}
