using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using ShuttleVNBackend.Application.DTOs.Authentication;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Core.Entities.User;
using ShuttleVNBackend.Core.Entities.User.Enums;
using ValidationException = ShuttleVNBackend.Application.Exceptions.ValidationException;

namespace ShuttleVNBackend.Application.UseCases.Authentication.Services;

public class AuthenticationService(
    IAccountRepository accountRepository,
    IVerificationCodeRepository verificationCodeRepository,
    IUnitOfWork unitOfWork)
{
    private readonly PasswordHasher<UserAccount> _hasher = new();
    
    public async Task<UserAccount> VerifyLogin(LoginDto dto)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(dto.Email))
            errors["Email"] = ["Email is required"];
        if (string.IsNullOrWhiteSpace(dto.Password))
            errors["Password"] = ["Password is required"];
        if (errors.Count > 0)
            throw new ValidationException(errors: errors);

        var account = await accountRepository.GetByEmailAsync(dto.Email);
        if (account is null || !IsValidPassword(account, dto.Password))
            throw new ValidationException("Invalid email or password");

        return account.Status is AccountStatus.Disabled
            ? throw new UnauthorizedException("This account is disabled. Please contact support.")
            : account;
    }

    public async Task<UserAccount> ResetPassword(ResetPasswordDto dto)
    {
        var account = await accountRepository.GetByEmailAsync(dto.Email);
        if (account is null)
            throw new ValidationException("Invalid email");

        if (!await IsValidVerificationCode(dto.Email, dto.Code, CodeType.ResetPassword))
            throw new ValidationException("Invalid or expired verification code");
        
        account.PasswordHash = _hasher.HashPassword(account, dto.Password);
        await unitOfWork.SaveChangesAsync();
        return account;

    }
    
    public async Task<string> IssueCode(string email, CodeType type)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));
        
        var existingCode = await verificationCodeRepository.GetActiveAsync(email, type);
        if (existingCode is not null && !existingCode.IsUsed)
            throw new InvalidOperationException("Already having an active code");
        
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
        await unitOfWork.AddAsync(code);
        await unitOfWork.SaveChangesAsync();
        
        return plainCode;
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

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(bytes);
    }
}