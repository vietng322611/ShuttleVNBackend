using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Users;
using ShuttleVNBackend.Application.UseCases.Users.Helpers;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Users;
using ShuttleVNBackend.Core.Entities.Users;
using ShuttleVNBackend.Core.Entities.Users.Enums;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories.Users;

public class CustomerRepository(ShuttleVnDbContext dbContext) : ICustomerRepository
{
    public async Task<PagedResult<Customer>> GetAllAsync(PageRequest page, CancellationToken ct = default)
    {
        var query = dbContext.Customers.OrderBy(x => x.CreatedAt);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PagedResult<Customer>
        {
            Items = items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await dbContext.Customers.FirstOrDefaultAsync(
            c => c.CustomerId == id, ct);
    }

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        return await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Email == email, ct);
    }

    public async Task<Customer?> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        return await dbContext.Customers.FirstOrDefaultAsync(c => c.AccountId == accountId, ct);
    }

    public async Task<PagedResult<CustomerListItemDto>> SearchAsync(string? keyword, PageRequest page,
        CancellationToken ct = default)
    {
        var customers = dbContext.Customers.AsNoTracking().AsQueryable();

        var term = keyword?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            var pattern = ToLikePattern(term);
            var phonePattern = ToLikePattern(CustomerProfileRules.NormalizePhone(term));
            customers = customers.Where(c =>
                EF.Functions.ILike(c.FullName, pattern)
                || EF.Functions.ILike(c.Email, pattern)
                || EF.Functions.ILike(c.Phone, phonePattern));
        }

        var totalCount = await customers.CountAsync(ct);
        var items = await Project(customers)
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .ToListAsync(ct);

        return new PagedResult<CustomerListItemDto>
        {
            Items = items,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<CustomerListItemDto?> GetListItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await Project(dbContext.Customers.AsNoTracking().Where(c => c.CustomerId == id))
            .FirstOrDefaultAsync(ct);
    }

    private IQueryable<CustomerListItemDto> Project(IQueryable<Customer> customers)
    {
        return from c in customers
            join a in dbContext.UserAccounts.AsNoTracking() on c.AccountId equals a.AccountId into accounts
            from a in accounts.DefaultIfEmpty()
            orderby c.CreatedAt, c.CustomerId
            select new CustomerListItemDto(
                c.CustomerId,
                c.FullName,
                c.Phone,
                c.Email,
                c.AccountId,
                c.AccountId != null,
                a != null ? (AccountStatus?)a.Status : null,
                c.CreatedAt,
                c.UpdatedAt);
    }

    private static string ToLikePattern(string term)
    {
        return $"%{term.Replace("\\", @"\\").Replace("%", "\\%").Replace("_", "\\_")}%";
    }
}