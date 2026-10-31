using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Interfaces.Repositories.Users;
using ShuttleVNBackend.Core.Entities.Users;
using ShuttleVNBackend.Core.Entities.Users.Enums;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories.Users;

public class VerificationCodeRepository(ShuttleVnDbContext dbContext) : IVerificationCodeRepository
{
    public async Task<VerificationCode?> GetActiveAsync(string email, CodeType type, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        return await dbContext.VerificationCodes
            .Where(code =>
                code.Email == email
                && code.Type == type
                && code.ExpiresAt > now
                && code.Attempt < 5 // might put this in config later
                && !code.IsUsed)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<VerificationCode?> GetAsync(string email, CodeType type, CancellationToken ct = default)
    {
        return await dbContext.VerificationCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(code => code.Email == email && code.Type == type, ct);
    }

    public async Task<bool> TryConsumeAsync(string email, CodeType type, string codeHash, DateTime nowUtc,
        CancellationToken ct = default)
    {
        var updated = await dbContext.VerificationCodes
            .Where(code =>
                code.Email == email
                && code.Type == type
                && code.CodeHash == codeHash
                && !code.IsUsed
                && code.ExpiresAt > nowUtc)
            .ExecuteUpdateAsync(setters => setters.SetProperty(code => code.IsUsed, true), ct);
        return updated == 1;
    }

    public async Task DeleteExistingAsync(string email, CodeType type)
    {
        var old = dbContext.VerificationCodes
            .Where(code => code.Email == email && code.Type == type);
        // staging this to put add and delete into 1 transaction
        dbContext.VerificationCodes.RemoveRange(old);
    }
}
