using Microsoft.AspNetCore.Identity;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Authentication;
using ShuttleVNBackend.Application.DTOs.Users;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Users;
using ShuttleVNBackend.Application.UseCases.Authentication.Services;
using ShuttleVNBackend.Core.Entities.Users;
using ShuttleVNBackend.Core.Entities.Users.Enums;

namespace ShuttleVNBackend.Application.UseCases.Users.Services;

public class AccountService(
    IAccountRepository accountRepository,
    ICustomerRepository customerRepository,
    IUnitOfWork unitOfWork,
    AppAuthService appAuthService)
{
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public async Task<UserAccount> Register(RegisterDto dto)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(dto.FullName))
            errors["FullName"] = ["Full name is required"];
        if (string.IsNullOrWhiteSpace(dto.Phone))
            errors["Phone"] = ["Phone number is required"];
        if (string.IsNullOrWhiteSpace(dto.Email))
            errors["Email"] = ["Email is required"];
        if (string.IsNullOrWhiteSpace(dto.Code))
            errors["Code"] = ["Verification code is required"];
        if (string.IsNullOrWhiteSpace(dto.Password))
            errors["Password"] = ["Password is required"];
        if (!string.IsNullOrWhiteSpace(dto.Password) &&
            !string.Equals(dto.Password, dto.ConfirmPassword))
            errors["ConfirmPassword"] = ["Passwords do not match"];
        if (errors.Count > 0)
            throw new ValidationException(errors: errors);

        var record = await accountRepository.GetByEmailAsync(dto.Email);
        if (record is not null)
            throw new ConflictException("Email is already registered");

        if (!await appAuthService.IsValidVerificationCode(dto.Email, dto.Code, CodeType.VerifyEmail))
            throw new ValidationException("Invalid or expired verification code");

        var now = DateTime.UtcNow;
        var account = new UserAccount
        {
            AccountId = Guid.NewGuid(),
            LoginEmail = dto.Email,
            AccountType = AccountType.Customer,
            Status = AccountStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        account.PasswordHash = _hasher.HashPassword(account, dto.Password);
        await unitOfWork.AddAsync(account);

        var existingCustomer = await customerRepository.GetByEmailAsync(dto.Email);
        if (existingCustomer is not null)
        {
            existingCustomer.AccountId = account.AccountId;
            existingCustomer.FullName = dto.FullName;
            existingCustomer.Phone = dto.Phone;
            existingCustomer.UpdatedAt = now;
        }
        else
        {
            var customer = new Customer
            {
                CustomerId = Guid.NewGuid(),
                AccountId = account.AccountId,
                FullName = dto.FullName,
                Phone = dto.Phone,
                Email = dto.Email,
                CreatedAt = now,
                UpdatedAt = now
            };
            await unitOfWork.AddAsync(customer);
        }

        await unitOfWork.SaveChangesAsync();
        return account;
    }

    public async Task<PagedResult<UserAccount>> GetAllAccounts(PageRequest page)
    {
        return await accountRepository.GetAllAsync(page);
    }

    public async Task<UserAccount> GetAccountById(Guid accountId)
    {
        return await accountRepository.GetByIdAsync(accountId)
               ?? throw new NotFoundException("Account not found");
    }

    public async Task UpdateAccountStatus(Guid accountId, AccountStatus status, bool isAdmin)
    {
        var account = await accountRepository.GetByIdAsync(accountId)
                      ?? throw new NotFoundException("Account not found");

        if (!isAdmin && account.AccountType != AccountType.Customer)
            throw new ForbiddenException("Staff can only manage customer accounts");

        if (account.Status == status) return;

        account.Status = status;
        if (status == AccountStatus.Active) account.FailedLoginAttempts = 0;
        account.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync();
    }

    public async Task ChangePassword(Guid accountId, ChangePasswordDto dto)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(dto.CurrentPassword))
            errors["CurrentPassword"] = ["Current password is required"];
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
            errors["NewPassword"] = ["New password must be at least 8 characters"];
        if (!string.Equals(dto.NewPassword, dto.ConfirmNewPassword))
            errors["ConfirmNewPassword"] = ["Passwords do not match"];
        if (errors.Count > 0)
            throw new ValidationException(errors: errors);

        var account = await accountRepository.GetByIdAsync(accountId)
                      ?? throw new NotFoundException("Account not found");

        var verifyResult = _hasher.VerifyHashedPassword(account, account.PasswordHash, dto.CurrentPassword);
        if (verifyResult == PasswordVerificationResult.Failed)
            throw new ValidationException("Current password is incorrect");

        account.PasswordHash = _hasher.HashPassword(account, dto.NewPassword);
        account.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync();
    }
}