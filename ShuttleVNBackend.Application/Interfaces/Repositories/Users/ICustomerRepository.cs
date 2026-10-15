using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Core.Entities.Users;
using ShuttleVNBackend.Application.DTOs.Users;

namespace ShuttleVNBackend.Application.Interfaces.Repositories.Users;

public interface ICustomerRepository
{
    public Task<PagedResult<Customer>> GetAllAsync(PageRequest page, CancellationToken ct = default);
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    public Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
    public Task<Customer?> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    public Task<PagedResult<CustomerListItemDto>> SearchAsync(string? keyword, PageRequest page,
        CancellationToken ct = default);
    public Task<CustomerListItemDto?> GetListItemByIdAsync(Guid id, CancellationToken ct = default);
}