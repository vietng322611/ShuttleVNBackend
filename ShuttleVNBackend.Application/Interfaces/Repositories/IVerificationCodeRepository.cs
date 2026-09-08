using ShuttleVNBackend.Core.Entities.User;
using ShuttleVNBackend.Core.Entities.User.Enums;

namespace ShuttleVNBackend.Application.Interfaces.Repositories;

public interface IVerificationCodeRepository
{
    Task<VerificationCode?> GetActiveAsync(Guid accountId, CodeType type, CancellationToken ct = default);
    Task DeleteExistingAsync(Guid accountId, CodeType type);
}