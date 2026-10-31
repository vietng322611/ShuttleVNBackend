using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using ShuttleVNBackend.Application.DTOs.Authentication;
using ShuttleVNBackend.Application.DTOs.Email;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Users;
using ShuttleVNBackend.Core.Entities.Users;
using ShuttleVNBackend.Core.Entities.Users.Enums;
using ValidationException = ShuttleVNBackend.Application.Exceptions.ValidationException;

namespace ShuttleVNBackend.Application.UseCases.Authentication.Services;

public class AppAuthService(
    IAccountRepository accountRepository,
    IVerificationCodeRepository verificationCodeRepository,
    IUnitOfWork unitOfWork,
    IEmailService emailService)
{
    private readonly PasswordHasher<UserAccount> _hasher = new();

    public async Task<UserAccount> VerifyLogin(LoginDto dto, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(dto.Email))
            errors["Email"] = ["Email is required"];
        if (string.IsNullOrWhiteSpace(dto.Password))
            errors["Password"] = ["Password is required"];
        if (errors.Count > 0)
            throw new ValidationException(errors: errors);

        var email = dto.Email.Trim().ToLowerInvariant();
        var account = await accountRepository.GetByEmailAsync(email, ct);
        if (account is null)
            throw new UnauthorizedException("Invalid email or password");

        if (account.Status is AccountStatus.Disabled)
            throw new UnauthorizedException("This account is disabled. Please contact support.");

        if (account.Status is AccountStatus.PendingActivation)
            throw new ForbiddenException(
                "This account has not been activated. Please set your password using the link sent to your email.");

        var passwordResult = _hasher.VerifyHashedPassword(account, account.PasswordHash, dto.Password);
        if (passwordResult == PasswordVerificationResult.Failed)
        {
            if (account.AccountType is AccountType.Customer)
            {
                account.FailedLoginAttempts++;
                if (account.FailedLoginAttempts >= 5)
                {
                    account.FailedLoginAttempts = 5;
                    account.Status = AccountStatus.Disabled;
                }

                account.UpdatedAt = DateTime.UtcNow;
                await unitOfWork.SaveChangesAsync(ct);
            }

            throw new UnauthorizedException("Invalid email or password");
        }

        if (account.Customer is null && account.Employee is null)
            throw new UnauthorizedException("Invalid account profile");

        account.FailedLoginAttempts = 0;
        account.UpdatedAt = DateTime.UtcNow;
        if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
            account.PasswordHash = _hasher.HashPassword(account, dto.Password);

        await unitOfWork.SaveChangesAsync(ct);
        return account;
    }

    public async Task<UserAccount> ResetPassword(ResetPasswordDto dto)
    {
        var account = await accountRepository.GetByEmailAsync(dto.Email);
        if (account is null)
            throw new ValidationException("Invalid email");

        if (account.Status is AccountStatus.PendingActivation)
            throw new ForbiddenException(
                "This account has not been activated. Please set your password using the link sent to your email.");

        if (!await IsValidVerificationCode(dto.Email, dto.Code, CodeType.ResetPassword))
            throw new ValidationException("Invalid or expired verification code");

        account.PasswordHash = _hasher.HashPassword(account, dto.Password);
        await unitOfWork.SaveChangesAsync();
        return account;
    }

    public async Task IssueCodeAsync(string email, CodeType type, CancellationToken ct = default)
    {
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            throw new ValidationException(errors: new Dictionary<string, string[]>
            {
                ["Email"] = ["A valid email address is required."]
            });
        }

        var purpose = type switch
        {
            CodeType.VerifyEmail => "email verification",
            CodeType.ResetPassword => "password reset",
            CodeType.QuickBooking => "quick booking verification",
            _ => throw new ValidationException(errors: new Dictionary<string, string[]>
            {
                ["Type"] = ["Invalid verification code type."]
            })
        };

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var existingCode = await verificationCodeRepository.GetActiveAsync(email, type, token);
            if (existingCode is not null && !existingCode.IsUsed)
            {
                throw new ConflictException("Already having an active code");
            }

            await verificationCodeRepository.DeleteExistingAsync(email, type);
            var plainCode = GenerateCode();
            var code = new VerificationCode
            {
                Email = email,
                Type = type,
                CodeHash = HashCode(plainCode),
                IsUsed = false,
                Attempt = 0,
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            };
            await unitOfWork.AddAsync(code, token);
            await unitOfWork.SaveChangesAsync(token);
            await emailService.SendAsync(new EmailMessage(email, $"ShuttleVN {purpose} code",
                $"Your {purpose} code is: {plainCode}\n\nThis code expires in 5 minutes. Do not share it with anyone."), token);
            return true;
        }, ct);
    }

    public async Task<bool> IsValidVerificationCode(string email, string code, CodeType type)
    {
        var now = DateTime.UtcNow;
        var existingCode = await verificationCodeRepository.GetActiveAsync(email, type);
        if (existingCode is null ||
            existingCode.IsUsed ||
            existingCode.ExpiresAt < now)
            return false;

        var hashedCode = HashCode(code);
        // plain string comparison exits early on the first mismatched character, which leaks tiny timing differences
        var match = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(hashedCode),
            Encoding.UTF8.GetBytes(existingCode.CodeHash));

        if (!match)
        {
            existingCode.Attempt++;
            await unitOfWork.SaveChangesAsync();
            return false;
        }

        existingCode.IsUsed = true;
        await unitOfWork.SaveChangesAsync();
        return true;
    }

    private bool IsValidPassword(UserAccount account, string password)
    {
        return _hasher.VerifyHashedPassword(
            account,
            account.PasswordHash,
            password) == PasswordVerificationResult.Success;
    }

    private static string GenerateCode()
    {
        var number = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return number.ToString("D6"); // zero-padded to 6 digits
    }

    internal static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }
}