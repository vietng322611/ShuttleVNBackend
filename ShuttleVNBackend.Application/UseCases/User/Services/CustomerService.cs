using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.DTOs.User;
using ShuttleVNBackend.Core.Entities.User;

namespace ShuttleVNBackend.Application.UseCases.User.Services;

public class CustomerService(
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<List<Customer>> GetAllCustomers(PageRequest page)
        => await customerRepository.GetAllCustomers(page);
    
    public async Task<Customer?> GetCustomerById(Guid id)
        => await customerRepository.GetCustomerById(id);

    public async Task<Customer> CreateCustomer(CustomerProfileDto dto)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(dto.FullName))
            errors["FullName"] = ["Full name is required"];
        if (string.IsNullOrWhiteSpace(dto.Phone))
            errors["Phone"] = ["Phone number is required"];
        if (string.IsNullOrWhiteSpace(dto.Email))
            errors["Email"] = ["Email is required"];
        if (errors.Count > 0)
            throw new ValidationException(errors: errors);

        var existing = await customerRepository.GetCustomerByEmail(dto.Email);
        if (existing is not null)
            throw new ConflictException("Email is already used by another customer");

        var now = DateTime.UtcNow;
        var customer = new Customer
        {
            CustomerId = Guid.NewGuid(),
            AccountId = null,
            FullName = dto.FullName,
            Phone = dto.Phone,
            Email = dto.Email,
            CreatedAt = now,
            UpdatedAt = now
        };

        await unitOfWork.AddAsync(customer);
        await unitOfWork.SaveChangesAsync();
        return customer;
    }

    public async Task<Customer> UpdateCustomer(Guid id, CustomerProfileDto dto)
    {
        var customer = await customerRepository.GetCustomerById(id)
                       ?? throw new NotFoundException("Customer not found");

        if (!string.IsNullOrWhiteSpace(dto.FullName))
            customer.FullName = dto.FullName;
        if (!string.IsNullOrWhiteSpace(dto.Phone))
            customer.Phone = dto.Phone;
        if (!string.IsNullOrWhiteSpace(dto.Email))
            customer.Email = dto.Email;

        customer.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync();
        return customer;
    }
}