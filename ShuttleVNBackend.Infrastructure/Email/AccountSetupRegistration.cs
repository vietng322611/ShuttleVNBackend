using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShuttleVNBackend.Application.Interfaces;

namespace ShuttleVNBackend.Infrastructure.Email;

public static class AccountSetupRegistration
{
    public static IServiceCollection AddAccountSetup(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AccountSetupOptions>()
            .Bind(configuration.GetSection(AccountSetupOptions.SectionName))
            .Validate(HasValidFrontendUrl, "AccountSetup:FrontendBaseUrl must be an absolute http or https URL.")
            .Validate(options => options.SetPasswordPath.StartsWith('/'),
                "AccountSetup:SetPasswordPath must start with '/'.");

        services.AddMemoryCache();
        services.AddSingleton<EmailTemplateRenderer>();
        services.AddSingleton<ISetupLinkRateLimiter, MemorySetupLinkRateLimiter>();
        services.AddScoped<IPasswordSetupMailer, PasswordSetupMailer>();
        return services;
    }

    private static bool HasValidFrontendUrl(AccountSetupOptions options)
    {
        return Uri.TryCreate(options.FrontendBaseUrl, UriKind.Absolute, out var uri)
               && uri.Scheme is "http" or "https";
    }
}

