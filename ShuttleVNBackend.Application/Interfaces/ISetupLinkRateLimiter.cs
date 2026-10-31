namespace ShuttleVNBackend.Application.Interfaces;

public interface ISetupLinkRateLimiter
{
    bool TryAcquire(string email, out TimeSpan retryAfter);
}

