using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Core.Entities.User;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories;

public class AccountRepository(ShuttleVnDbContext dbContext): IAccountRepository
{
    public async Task<List<UserAccount>> GetAllAsync(PageRequest page, CancellationToken ct = default)
    {
        var skip = (page.PageNumber - 1) * page.PageSize;

        return await dbContext.UserAccounts
            .OrderBy(x => x.CreatedAt)
            .Skip(skip)
            .Take(page.PageSize)
            .ToListAsync(ct);
    }

    public async Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.UserAccounts.FirstOrDefaultAsync(
            x => x.AccountId == id, ct);
    }

    public async Task<UserAccount?> GetByUsernameOrEmailAsync(string username, CancellationToken ct = default)
    {
        return await dbContext.UserAccounts
            .Where(u =>
                u.Username == username ||
                u.LoginEmail == username)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UserAccount> AddAsync(UserAccount userAccount, CancellationToken ct = default)
    {
        var newEntity = await dbContext.UserAccounts.AddAsync(userAccount, ct);
        return newEntity.Entity;
    }
}