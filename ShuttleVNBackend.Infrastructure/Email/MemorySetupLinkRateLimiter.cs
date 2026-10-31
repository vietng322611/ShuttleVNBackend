using Microsoft.Extensions.Caching.Memory;
using ShuttleVNBackend.Application.Interfaces;
using ShuttleVNBackend.Application.UseCases.Authentication.Helpers;

namespace ShuttleVNBackend.Infrastructure.Email;

public sealed class MemorySetupLinkRateLimiter(IMemoryCache cache, TimeProvider clock) : ISetupLinkRateLimiter
{
    private readonly Lock _gate = new();

    public bool TryAcquire(string email, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        var now = clock.GetUtcNow().UtcDateTime;
        var key = $"setup-link:{email}";

        lock (_gate)
        {
            var attempts = cache.TryGetValue(key, out List<DateTime>? stored) && stored is not null
                ? stored
                : new List<DateTime>();
            attempts.RemoveAll(at => now - at >= PasswordSetupRules.ResendWindow);

            if (attempts.Count > 0 && now - attempts[^1] < PasswordSetupRules.ResendCooldown)
            {
                retryAfter = PasswordSetupRules.ResendCooldown - (now - attempts[^1]);
                return false;
            }

            if (attempts.Count >= PasswordSetupRules.ResendMaxPerWindow)
            {
                retryAfter = attempts[0] + PasswordSetupRules.ResendWindow - now;
                return false;
            }

            attempts.Add(now);
            cache.Set(key, attempts, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = PasswordSetupRules.ResendWindow
            });
            return true;
        }
    }
}

