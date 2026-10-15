using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.DTOs.Users;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.Interfaces.Repositories.Users;
using ShuttleVNBackend.Application.UseCases.Bookings.Helpers;
using ShuttleVNBackend.Application.UseCases.Users.Helpers;
using ShuttleVNBackend.Core.Entities.Users;

namespace ShuttleVNBackend.Application.UseCases.Users.Services;

public class CustomerService(
    ICustomerRepository customerRepository,
    IBookingRepository bookingRepository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<PagedResult<CustomerListItemDto>> SearchAsync(string? keyword, PageRequest page,
        CancellationToken ct = default)
    {
        return await customerRepository.SearchAsync(keyword, page, ct);
    }

    public async Task<CustomerListItemDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await customerRepository.GetListItemByIdAsync(id, ct)
               ?? throw new NotFoundException("Customer not found");
    }

    public async Task<CustomerListItemDto> CreateCustomer(CustomerProfileDto dto, CancellationToken ct = default)
    {
        var profile = CustomerProfileRules.Normalize(dto, true);

        if (await customerRepository.GetByEmailAsync(profile.Email, ct) is not null)
            throw new ConflictException("Email is already used by another customer");

        var now = clock.GetUtcNow().UtcDateTime;
        var customer = new Customer
        {
            CustomerId = Guid.NewGuid(),
            AccountId = null,
            FullName = profile.FullName,
            Phone = profile.Phone,
            Email = profile.Email,
            CreatedAt = now,
            UpdatedAt = now
        };

        await unitOfWork.AddAsync(customer, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return await GetByIdAsync(customer.CustomerId, ct);
    }

    public async Task<CustomerListItemDto> UpdateCustomer(Guid id, CustomerProfileDto dto,
        CancellationToken ct = default)
    {
        var profile = CustomerProfileRules.Normalize(dto, false);

        var customer = await customerRepository.GetByIdAsync(id, ct)
                       ?? throw new NotFoundException("Customer not found");

        if (profile.Email.Length > 0 && profile.Email != CustomerProfileRules.NormalizeEmail(customer.Email))
            throw new ValidationException(errors: new Dictionary<string, string[]>
                { ["Email"] = ["Email cannot be changed here; use the change-email flow"] });

        if (customer.FullName != profile.FullName || customer.Phone != profile.Phone)
        {
            customer.FullName = profile.FullName;
            customer.Phone = profile.Phone;
            customer.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            await unitOfWork.SaveChangesAsync(ct);
        }

        return await GetByIdAsync(id, ct);
    }

    public async Task<PagedResult<BookingListItemDto>> GetBookingsAsync(Guid customerId, PageRequest page,
        CancellationToken ct = default)
    {
        if (await customerRepository.GetByIdAsync(customerId, ct) is null)
            throw new NotFoundException("Customer not found");

        var rows = await bookingRepository.GetByCustomerAsync(customerId, page, ct);
        return new PagedResult<BookingListItemDto>
        {
            Items = rows.Items.Select(r => r.ToListItem()).ToList(),
            PageNumber = rows.PageNumber,
            PageSize = rows.PageSize,
            TotalCount = rows.TotalCount
        };
    }
}