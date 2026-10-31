namespace ShuttleVNBackend.Infrastructure.Email;

public sealed class AccountSetupOptions
{
    public const string SectionName = "AccountSetup";

    public string FrontendBaseUrl { get; set; } = string.Empty;
    public string SetPasswordPath { get; set; } = "/set-password";
}

