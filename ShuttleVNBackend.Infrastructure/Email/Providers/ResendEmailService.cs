using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MimeKit;
using ShuttleVNBackend.Application.DTOs.Email;
using ShuttleVNBackend.Application.Interfaces;
using ShuttleVNBackend.Infrastructure.Email.Options;

namespace ShuttleVNBackend.Infrastructure.Email.Providers;

public sealed class ResendEmailService(HttpClient httpClient, IOptions<EmailServiceOptions> options) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Headers.UserAgent.ParseAdd("ShuttleVN/1.0");
        request.Content = JsonContent.Create(new
        {
            from = new MailboxAddress(settings.FromName, settings.FromAddress).ToString(),
            to = new[] { message.To },
            subject = message.Subject,
            html = message.IsHtml ? message.Body : null,
            text = message.IsHtml ? null : message.Body
        }, options: new System.Text.Json.JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("Email delivery failed. Resend rejected the request.");
        }
        catch (Exception exception) when (
            exception is HttpRequestException
                or OperationCanceledException
            && !cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException("Email delivery failed. Resend is unavailable or timed out.");
        }
    }
}
