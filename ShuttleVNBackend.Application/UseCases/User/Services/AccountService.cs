using Microsoft.AspNetCore.Identity;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Authentication;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Core.Entities.User;
using ShuttleVNBackend.Core.Entities.User.Enums;
using ShuttleVNBackend.Application.UseCases.Authentication.Services;

namespace ShuttleVNBackend.Application.UseCases.User.Services;

public class AccountService(
    IAccountRepository accountRepository,
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
        await unitOfWork.SaveChangesAsync();
        
        return account;
    }

    public async Task<PagedResult<UserAccount>> GetAllAccounts(PageRequest page)
        => await accountRepository.GetAllAsync(page);
    
    public async Task UpdateAccountStatus(Guid accountId, AccountStatus status)
    {
        var account = await accountRepository.GetByIdAsync(accountId);
        if (account is null)
            throw new NotFoundException("Account not found");
        
        account.Status = status;
        await unitOfWork.SaveChangesAsync();
    }
}