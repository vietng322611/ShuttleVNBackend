using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Core.Entities.User;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories;

public class CustomerRepository(ShuttleVnDbContext dbContext): ICustomerRepository
{
    public async Task<PagedResult<Customer>> GetAllCustomers(PageRequest page, CancellationToken ct = default)
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

    public async Task<Customer?> GetCustomerById(Guid id, CancellationToken ct = default)
        => await dbContext.Customers.FirstOrDefaultAsync(
            c => c.CustomerId == id, ct);

    public async Task<Customer?> GetCustomerByEmail(string email, CancellationToken ct = default)
        => await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Email == email, ct);
}