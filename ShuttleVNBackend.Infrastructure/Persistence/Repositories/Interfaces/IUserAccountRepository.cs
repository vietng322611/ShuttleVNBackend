using ShuttleVNBackend.Core.Entities.User;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories.Interfaces;

public interface IUserAccountRepository
{
    Task<List<UserAccount>> GetAllAsync();
    Task<UserAccount?> GetByIdAsync(Guid id);
    Task<UserAccount?> GetByUsernameOrEmailAsync(string username);
    Task<UserAccount> AddAsync(UserAccount userAccount);
    Task SoftDeleteAsync(Guid id);
}