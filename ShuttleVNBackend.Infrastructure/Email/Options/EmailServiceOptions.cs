namespace ShuttleVNBackend.Infrastructure.Email.Options;

public sealed class EmailServiceOptions
{
    public const string SectionName = "EmailService";

    public string Provider { get; set; } = "Resend";
    public string ApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "ShuttleVN";
    public int TimeoutSeconds { get; set; } = 30;
    public SmtpOptions Smtp { get; set; } = new();
}