using ShuttleVNBackend.Core.Entities.User;
using ShuttleVNBackend.Core.Entities.User.Enums;

namespace ShuttleVNBackend.Application.Interfaces.Repositories;

public interface IVerificationCodeRepository
{
    Task<VerificationCode?> GetActiveAsync(string email, CodeType type, CancellationToken ct = default);
    Task DeleteExistingAsync(string email, CodeType type);
}