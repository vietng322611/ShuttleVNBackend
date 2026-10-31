using System.Buffers.Text;
using System.Net.Mail;
using System.Security.Cryptography;

namespace ShuttleVNBackend.Application.UseCases.Authentication.Helpers;

public static class PasswordSetupRules
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan ResendWindow = TimeSpan.FromHours(1);

    public const int ResendMaxPerWindow = 5;
    public const int TokenByteLength = 32;
    public const int TokenMaxLength = 128;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;

    public static string GenerateToken()
    {
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenByteLength));
    }

    public static bool IsValidEmail(string email)
    {
        return email.Length is > 0 and <= 254
               && MailAddress.TryCreate(email, out var address)
               && address.Address == email;
    }
}

