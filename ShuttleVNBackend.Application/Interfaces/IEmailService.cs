using ShuttleVNBackend.Application.DTOs.Email;

namespace ShuttleVNBackend.Application.Interfaces;

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}