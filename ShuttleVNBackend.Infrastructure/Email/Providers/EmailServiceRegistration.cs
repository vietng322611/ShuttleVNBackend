using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShuttleVNBackend.Application.Interfaces;
using ShuttleVNBackend.Infrastructure.Email.Options;

namespace ShuttleVNBackend.Infrastructure.Email.Providers;

public static class EmailServiceRegistration
{
    public static IServiceCollection AddEmailService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailServiceOptions>()
            .Bind(configuration.GetSection(EmailServiceOptions.SectionName))
            .Validate(options => options.Provider is "Resend" or "Smtp", "EmailService:Provider must be Resend or Smtp.")
            .Validate(HasValidSenderAddress, "EmailService:FromAddress must be a valid email address.")
            .Validate(options => !options.FromName.Contains('\r') && !options.FromName.Contains('\n'),
                "EmailService:FromName cannot contain line breaks.")
            .Validate(options => options.TimeoutSeconds is >= 1 and <= 300,
                "EmailService:TimeoutSeconds must be between 1 and 300.")
            .Validate(options => options.Provider != "Resend" || !string.IsNullOrWhiteSpace(options.ApiKey),
                "EmailService:ApiKey is required for Resend.")
            .Validate(HasValidSmtpSettings,
                "SMTP requires a host, valid port, and either both credentials or neither.")
            .ValidateOnStart();

        services.AddHttpClient<ResendEmailService>();
        services.AddTransient<SmtpEmailService>();
        services.AddTransient<IEmailService>(provider =>
            provider.GetRequiredService<IOptions<EmailServiceOptions>>().Value.Provider switch
            {
                "Resend" => provider.GetRequiredService<ResendEmailService>(),
                "Smtp" => provider.GetRequiredService<SmtpEmailService>(),
                _ => throw new InvalidOperationException("Unsupported email provider.")
            });
        return services;
    }

    private static bool HasValidSenderAddress(EmailServiceOptions options)
    {
        return MailAddress.TryCreate(options.FromAddress, out var address)
               && address.Address == options.FromAddress;
    }

    private static bool HasValidSmtpSettings(EmailServiceOptions options)
    {
        if (options.Provider != "Smtp")
            return true; // skip checking SMTP because it's not used

        var smtp = options.Smtp;
        var hasUsername = !string.IsNullOrWhiteSpace(smtp.Username);
        var hasPassword = !string.IsNullOrWhiteSpace(smtp.Password);

        return !string.IsNullOrWhiteSpace(smtp.Host)
               && smtp.Port is >= 1 and <= 65535
               && hasUsername == hasPassword;
    }
}
