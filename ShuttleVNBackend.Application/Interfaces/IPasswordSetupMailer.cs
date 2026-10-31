namespace ShuttleVNBackend.Application.Interfaces;

public interface IPasswordSetupMailer
{
    Task SendAsync(string email, string fullName, string token, CancellationToken ct = default);
}

