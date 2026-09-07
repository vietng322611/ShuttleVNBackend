using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Core.Entities.User;

namespace ShuttleVNBackend.Application.Interfaces.Repositories;

public interface IUserAccountRepository
{
    Task<List<UserAccount>> GetAllAsync(PageRequest page, CancellationToken ct = default);
    Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserAccount?> GetByUsernameOrEmailAsync(string username, CancellationToken ct = default);
    Task<UserAccount> AddAsync(UserAccount userAccount, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}