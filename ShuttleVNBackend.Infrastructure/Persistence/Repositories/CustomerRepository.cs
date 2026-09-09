using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Core.Entities.User;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories;

public class CustomerRepository(ShuttleVnDbContext dbContext): ICustomerRepository
{
    public async Task<List<Customer>> GetAllCustomers(PageRequest page, CancellationToken ct = default)
    {
        var skip = (page.PageNumber - 1) * page.PageSize;
        return await dbContext.Customers
            .OrderBy(x => x.CreatedAt)
            .Skip(skip)
            .Take(page.PageSize)
            .ToListAsync(ct);
    }

    public async Task<Customer?> GetCustomerById(Guid id, CancellationToken ct = default)
        => await dbContext.Customers.FirstOrDefaultAsync(
            c => c.CustomerId == id, ct);

    public async Task<Customer?> GetCustomerByEmail(string email, CancellationToken ct = default)
        => await dbContext.Customers
            .FirstOrDefaultAsync(c => c.Email == email, ct);
}