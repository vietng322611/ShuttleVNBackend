using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using ShuttleVNBackend.Application.DTOs.Authentication;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Users;
using ShuttleVNBackend.Application.UseCases.Authentication.Helpers;
using ShuttleVNBackend.Application.UseCases.Users.Helpers;
using ShuttleVNBackend.Core.Entities.Users;
using ShuttleVNBackend.Core.Entities.Users.Enums;

namespace ShuttleVNBackend.Application.UseCases.Authentication.Services;

public class PasswordSetupService(
    IAccountRepository accountRepository,
    IVerificationCodeRepository verificationCodeRepository,
    IUnitOfWork unitOfWork,
    IPasswordSetupMailer mailer,
    ISetupLinkRateLimiter rateLimiter,
    TimeProvider clock)
{
    private const string InvalidLinkMessage = "Link is invalid or has expired";

    private readonly PasswordHasher<UserAccount> _hasher = new();

    public async Task IssueAsync(string email, string fullName, CancellationToken ct = default)
    {
        var token = PasswordSetupRules.GenerateToken();

        await verificationCodeRepository.DeleteExistingAsync(email, CodeType.SetPassword);
        await unitOfWork.AddAsync(new VerificationCode
        {
            Email = email,
            Type = CodeType.SetPassword,
            CodeHash = AppAuthService.HashCode(token),
            Attempt = 0,
            IsUsed = false,
            ExpiresAt = clock.GetUtcNow().UtcDateTime.Add(PasswordSetupRules.TokenLifetime)
        }, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await mailer.SendAsync(email, fullName, token, ct);
    }

    public async Task SetPasswordAsync(SetPasswordDto dto, CancellationToken ct = default)
    {
        var email = CustomerProfileRules.NormalizeEmail(dto.Email ?? string.Empty);
        ValidateSetPassword(dto, email);
        var tokenHash = AppAuthService.HashCode(dto.Token);

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var now = clock.GetUtcNow().UtcDateTime;

            var stored = await verificationCodeRepository.GetAsync(email, CodeType.SetPassword, token);
            if (stored is null || stored.IsUsed || stored.ExpiresAt <= now ||
                !HashesMatch(tokenHash, stored.CodeHash))
                throw InvalidLink();

            var account = await accountRepository.GetByEmailAsync(email, token);
            if (account is null ||
                account.AccountType != AccountType.Customer ||
                account.Status != AccountStatus.PendingActivation)
                throw InvalidLink();

            if (!await verificationCodeRepository.TryConsumeAsync(
                    email, CodeType.SetPassword, stored.CodeHash, now, token))
                throw InvalidLink();

            account.PasswordHash = _hasher.HashPassword(account, dto.Password);
            account.Status = AccountStatus.Active;
            account.FailedLoginAttempts = 0;
            account.UpdatedAt = now;
            await unitOfWork.SaveChangesAsync(token);
            return true;
        }, ct);
    }

    public async Task ResendAsync(ResendSetupLinkDto dto, CancellationToken ct = default)
    {
        var email = CustomerProfileRules.NormalizeEmail(dto.Email ?? string.Empty);
        if (!PasswordSetupRules.IsValidEmail(email))
            throw new ValidationException(errors: new Dictionary<string, string[]>
            {
                ["Email"] = ["A valid email address is required"]
            });

        if (!rateLimiter.TryAcquire(email, out var retryAfter))
            throw new TooManyRequestsException(retryAfter);

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var account = await accountRepository.GetByEmailAsync(email, token);
            var customer = account?.Customer;
            if (account is null || customer is null ||
                account.AccountType != AccountType.Customer ||
                account.Status != AccountStatus.PendingActivation)
                return false;

            await IssueAsync(email, customer.FullName, token);
            return true;
        }, ct);
    }

    private static void ValidateSetPassword(SetPasswordDto dto, string email)
    {
        var errors = new Dictionary<string, string[]>();

        if (!PasswordSetupRules.IsValidEmail(email))
            errors["Email"] = ["A valid email address is required"];
        if (string.IsNullOrWhiteSpace(dto.Token) || dto.Token.Length > PasswordSetupRules.TokenMaxLength)
            errors["Token"] = [InvalidLinkMessage];
        if (string.IsNullOrEmpty(dto.Password) ||
            dto.Password.Length is < PasswordSetupRules.PasswordMinLength or > PasswordSetupRules.PasswordMaxLength)
            errors["Password"] =
            [
                $"Password must be between {PasswordSetupRules.PasswordMinLength} and {PasswordSetupRules.PasswordMaxLength} characters"
            ];
        else if (!string.Equals(dto.Password, dto.ConfirmPassword, StringComparison.Ordinal))
            errors["ConfirmPassword"] = ["Passwords do not match"];

        if (errors.Count > 0)
            throw new ValidationException(errors: errors);
    }

    private static bool HashesMatch(string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right));
    }

    private static ValidationException InvalidLink()
    {
        return new ValidationException(InvalidLinkMessage, new Dictionary<string, string[]>
        {
            ["Token"] = [InvalidLinkMessage]
        });
    }
}

