using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Core.Entities.User;

namespace ShuttleVNBackend.Application.Interfaces.Repositories;

public interface ICustomerRepository
{
    public Task<PagedResult<Customer>> GetAllCustomers(PageRequest page, CancellationToken ct = default);
    public Task<Customer?> GetCustomerById(Guid id, CancellationToken ct = default);
    public Task<Customer?> GetCustomerByEmail(string email, CancellationToken ct = default);
}