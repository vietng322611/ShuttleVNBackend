using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Core.Entities.User;
using ShuttleVNBackend.Core.Entities.User.Enums;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories;

public class VerificationCodeRepository(ShuttleVnDbContext dbContext): IVerificationCodeRepository
{
    public async Task<VerificationCode?> GetActiveAsync(Guid accountId, CodeType type, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        
        return await dbContext.VerificationCodes
            .Where(code => 
                code.AccountId == accountId
                && code.Type == type
                && code.ExpiresAt > now
                && code.Attempt < 5 // might put this in config later
                && !code.IsUsed)
            .FirstOrDefaultAsync(ct);
    }

    public async Task DeleteExistingAsync(Guid accountId, CodeType type)
    {
        var old = dbContext.VerificationCodes
            .Where(code => code.AccountId == accountId && code.Type == type);
        // staging this to put add and delete into 1 transaction
        dbContext.VerificationCodes.RemoveRange(old);
    }
}