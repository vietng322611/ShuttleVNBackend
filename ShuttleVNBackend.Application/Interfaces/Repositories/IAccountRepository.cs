using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Core.Entities.User;

namespace ShuttleVNBackend.Application.Interfaces.Repositories;

public interface IAccountRepository
{
    Task<List<UserAccount>> GetAllAsync(PageRequest page, CancellationToken ct = default);
    Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserAccount?> GetByEmailAsync(string email, CancellationToken ct = default);
}