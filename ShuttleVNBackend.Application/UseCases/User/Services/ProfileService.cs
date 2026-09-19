using ShuttleVNBackend.Application.DTOs.User;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Core.Entities.User.Enums;
using ShuttleVNBackend.Core.Entities.User;

namespace ShuttleVNBackend.Application.UseCases.User.Services;

public class ProfileService(
    IAccountRepository accountRepository,
    ICustomerRepository customerRepository,
    IEmployeeRepository employeeRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<UserAccount> UpdateProfile(Guid accountId, AccountType accountType, UpdateProfileDto dto)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(dto.FullName)) errors["FullName"] = ["Full name is required"];
        if (string.IsNullOrWhiteSpace(dto.Phone)) errors["Phone"] = ["Phone number is required"];
        if (errors.Count > 0) throw new ValidationException(errors: errors);

        var now = DateTime.UtcNow;
        if (accountType == AccountType.Customer)
        {
            var customer = await customerRepository.GetByAccountIdAsync(accountId)
                          ?? throw new NotFoundException("Profile not found");
            customer.FullName = dto.FullName;
            customer.Phone = dto.Phone;
            customer.UpdatedAt = now;
        }
        else
        {
            var employeeAccount = await employeeRepository.GetByAccountIdAsync(accountId)
                                  ?? throw new NotFoundException("Profile not found");
            var employee = employeeAccount.Employee ?? throw new NotFoundException("Profile not found");
            employee.FullName = dto.FullName;
            employee.Phone = dto.Phone;
            employee.UpdatedAt = now;
        }

        await unitOfWork.SaveChangesAsync();
        return await accountRepository.GetByIdAsync(accountId)
               ?? throw new NotFoundException("Account not found");
    }
}