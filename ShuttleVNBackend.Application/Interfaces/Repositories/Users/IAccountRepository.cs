using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Core.Entities.Users;

namespace ShuttleVNBackend.Application.Interfaces.Repositories.Users;

public interface IAccountRepository
{
    Task<PagedResult<UserAccount>> GetAllAsync(PageRequest page, CancellationToken ct = default);
    Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserAccount?> GetByEmailAsync(string email, CancellationToken ct = default);
}