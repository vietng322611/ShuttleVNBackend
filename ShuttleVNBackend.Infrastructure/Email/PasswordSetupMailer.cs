using System.Globalization;
using Microsoft.Extensions.Options;
using ShuttleVNBackend.Application.DTOs.Email;
using ShuttleVNBackend.Application.Interfaces;
using ShuttleVNBackend.Application.UseCases.Authentication.Helpers;

namespace ShuttleVNBackend.Infrastructure.Email;

public sealed class PasswordSetupMailer(
    IEmailService emailService,
    EmailTemplateRenderer renderer,
    IOptions<AccountSetupOptions> options) : IPasswordSetupMailer
{
    public async Task SendAsync(string email, string fullName, string token, CancellationToken ct = default)
    {
        var settings = options.Value;
        var link = $"{settings.FrontendBaseUrl.TrimEnd('/')}{settings.SetPasswordPath}" +
                   $"?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
        var hours = ((int)PasswordSetupRules.TokenLifetime.TotalHours).ToString(CultureInfo.InvariantCulture);

        var body = renderer.Render(
            "set-password",
            "Thiết lập mật khẩu ShuttleVN",
            $"Liên kết thiết lập mật khẩu có hiệu lực trong {hours} giờ.",
            new Dictionary<string, string>
            {
                ["FullName"] = fullName,
                ["SetPasswordUrl"] = link,
                ["ExpirationHours"] = hours
            });

        await emailService.SendAsync(
            new EmailMessage(email, "ShuttleVN: Thiết lập mật khẩu cho tài khoản của bạn", body, isHtml: true),
            ct);
    }
}

