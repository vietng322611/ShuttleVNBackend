using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Core.Entities.User;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories;

public class AccountRepository(ShuttleVnDbContext dbContext): IAccountRepository
{
    public async Task<PagedResult<UserAccount>> GetAllAsync(PageRequest page, CancellationToken ct = default)
    {
        var query = dbContext.UserAccounts.OrderBy(x => x.CreatedAt);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PagedResult<UserAccount>
        {
            Items = items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<UserAccount?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.UserAccounts.FirstOrDefaultAsync(
            x => x.AccountId == id, ct);
    }

    public async Task<UserAccount?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await dbContext.UserAccounts
            .Where(u => u.LoginEmail == email)
            .FirstOrDefaultAsync(ct);
    }
}