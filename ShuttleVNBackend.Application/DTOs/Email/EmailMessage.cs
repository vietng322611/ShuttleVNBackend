using System.Net.Mail;

namespace ShuttleVNBackend.Application.DTOs.Email;

public sealed record EmailMessage
{
    public EmailMessage(string to, string subject, string body, bool isHtml = false)
    {
        if (!MailAddress.TryCreate(to, out var address) || address.Address != to)
            throw new ArgumentException("A single valid recipient email address is required.", nameof(to));

        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        if (subject.Contains('\r') || subject.Contains('\n'))
            throw new ArgumentException("Email subjects cannot contain line breaks.", nameof(subject));

        To = to;
        Subject = subject;
        Body = body;
        IsHtml = isHtml;
    }

    public string To { get; }
    public string Subject { get; }
    public string Body { get; }
    public bool IsHtml { get; }
}