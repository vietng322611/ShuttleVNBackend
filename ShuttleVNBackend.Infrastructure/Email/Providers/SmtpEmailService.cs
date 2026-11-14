using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using ShuttleVNBackend.Application.DTOs.Email;
using ShuttleVNBackend.Application.Interfaces;
using ShuttleVNBackend.Infrastructure.Email.Options;

namespace ShuttleVNBackend.Infrastructure.Email.Providers;

public sealed class SmtpEmailService(IOptions<EmailServiceOptions> options) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        using var email = new MimeMessage();
        email.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        email.To.Add(new MailboxAddress(string.Empty, message.To));
        email.Subject = message.Subject;
        email.Body = new TextPart(message.IsHtml ? "html" : "plain") { Text = message.Body };

        using var client = new SmtpClient();
        client.Timeout = settings.TimeoutSeconds * 1000;
        try
        {
            await client.ConnectAsync(
                settings.Smtp.Host,
                settings.Smtp.Port,
                settings.Smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
                timeout.Token);
            if (!string.IsNullOrWhiteSpace(settings.Smtp.Username))
            {
                await client.AuthenticateAsync(settings.Smtp.Username, settings.Smtp.Password, timeout.Token);
            }

            await client.SendAsync(email, timeout.Token);
        }
        catch (Exception e) when (
            e is IOException
                or ProtocolException
                or CommandException
                or AuthenticationException
                or System.Security.Authentication.AuthenticationException
                or SocketException
                or OperationCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Email delivery failed. SMTP rejected the request or is unavailable.");
        }
    }
}
