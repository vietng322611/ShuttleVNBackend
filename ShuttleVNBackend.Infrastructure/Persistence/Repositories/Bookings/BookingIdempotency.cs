using System.Buffers.Binary;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Core.Entities.System;
using ShuttleVNBackend.Core.Entities.Users.Enums;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories.Bookings;

public sealed class BookingIdempotency(
    ShuttleVnDbContext db,
    IUnitOfWork unitOfWork,
    IHttpContextAccessor accessor,
    IDataProtectionProvider protection,
    TimeProvider clock) : IBookingIdempotency
{
    private const string CookieName = "shuttlevn.booking-scope";
    private static readonly TimeSpan GuestScopeLifetime = TimeSpan.FromDays(30);

    private readonly ITimeLimitedDataProtector _protector = protection
        .CreateProtector("ShuttleVN.BookingIdempotency.v2").ToTimeLimitedDataProtector();

    public string GetScope(bool guest = false, bool requireEstablished = false)
    {
        var context = accessor.HttpContext ?? throw new UnauthorizedException();
        if (!guest)
        {
            var account = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (context.User.Identity?.IsAuthenticated != true || !Guid.TryParse(account, out var accountId))
                throw new UnauthorizedException();

            return $"account:{accountId:D}";
        }

        if (context.Request.Cookies.TryGetValue(CookieName, out var cookie))
            try
            {
                var value = _protector.Unprotect(cookie, out var expiresAt);
                if (expiresAt > clock.GetUtcNow() && Guid.TryParseExact(value, "N", out var id)) return $"guest:{id:N}";
            }
            catch (CryptographicException)
            {
                // Invalid cookies never select an existing guest's scope.
            }

        if (requireEstablished)
            throw new ValidationException("Request a price preview first to establish the guest booking scope.",
                new Dictionary<string, string[]>
                    { ["GuestScope"] = ["Request a price preview first and retain its cookie."] });

        var scope = Guid.NewGuid().ToString("N");
        context.Response.Cookies.Append(CookieName,
            _protector.Protect(scope, clock.GetUtcNow().Add(GuestScopeLifetime)), new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // could cause problem when using localhost
                SameSite = context.Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
                IsEssential = true,
                Path = "/api",
                MaxAge = GuestScopeLifetime
            });
        return $"guest:{scope}";
    }

    public async Task LockVerificationAsync(string normalizedEmail, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Verification locks require an active transaction.");

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"booking.quick.verification:{normalizedEmail}"));
        var lockId = BinaryPrimitives.ReadInt64BigEndian(bytes);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", ct);
    }

    public async Task<BookingMutationResponse<T>> ExecuteAsync<T>(
        string scope, string key, string operation, Guid? bookingId, object payload,
        Func<CancellationToken, Task> authorize,
        Func<CancellationToken, Task<BookingMutationResponse<T>>> action,
        CancellationToken ct = default)
    {
        ValidateKey(key);
        var requestHash = HashRequest(operation, bookingId, payload);
        var lockBytes = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { scope, key })));
        var lockId = BinaryPrimitives.ReadInt64BigEndian(lockBytes);

        return await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", token);
            await ValidateAccountAsync(scope, operation, token);
            await authorize(token);
            var stored = await db.IdempotencyRequests.AsNoTracking()
                .SingleOrDefaultAsync(r => r.ActorScope == scope && r.RequestKey == key, token);
            if (stored is not null)
            {
                if (stored.Operation != operation || stored.RequestHash != requestHash ||
                    (stored.BookingId != bookingId && bookingId is not null))
                    throw new ConflictException("Idempotency-Key was already used for a different request.");

                return JsonSerializer.Deserialize<BookingMutationResponse<T>>(stored.ResponseBody)
                       ?? throw new InvalidOperationException("Stored booking response is invalid.");
            }

            var response = await action(token);
            await unitOfWork.AddAsync(new IdempotencyRequest
            {
                Id = Guid.NewGuid(),
                ActorScope = scope,
                RequestKey = key,
                Operation = operation,
                RequestHash = requestHash,
                BookingId = bookingId ?? response.BookingId,
                ResponseStatus = response.ValidationErrors is not null ? StatusCodes.Status400BadRequest
                    : response.Conflict is null ? StatusCodes.Status200OK : StatusCodes.Status409Conflict,
                ResponseBody = JsonSerializer.Serialize(response),
                CreatedAt = clock.GetUtcNow().UtcDateTime
            }, token);
            await unitOfWork.SaveChangesAsync(token);
            return response;
        }, ct);
    }

    private async Task ValidateAccountAsync(string scope, string operation, CancellationToken ct)
    {
        if (!scope.StartsWith("account:", StringComparison.Ordinal))
        {
            if (operation != "booking.quick")
                throw new UnauthorizedException();

            return;
        }

        var user = accessor.HttpContext?.User ?? throw new UnauthorizedException();
        if (user.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId) ||
            scope != $"account:{accountId:D}")
            throw new UnauthorizedException();

        var account = await db.UserAccounts.AsNoTracking()
            .Include(a => a.Customer).Include(a => a.Employee)
            .SingleOrDefaultAsync(a => a.AccountId == accountId, ct);
        if (account is null || account.Status != AccountStatus.Active)
            throw new UnauthorizedException("Account is not active.");

        var role = account.GetRole();
        var actorClaim = account.AccountType == AccountType.Customer ? "CustomerId" : "EmployeeId";
        var actorId = account.AccountType == AccountType.Customer
            ? account.Customer?.CustomerId
            : account.Employee?.EmployeeId;
        if (!user.IsInRole(role) || actorId is null ||
            !Guid.TryParse(user.FindFirstValue(actorClaim), out var claimedActor) || claimedActor != actorId ||
            user.FindFirst(actorClaim == "CustomerId" ? "EmployeeId" : "CustomerId") is not null ||
            (operation == "booking.create" && account.AccountType != AccountType.Customer) ||
            (operation is "booking.confirm" or "booking.reject" or "booking.complete" &&
             account.AccountType != AccountType.Employee))
            throw new ForbiddenException("Account role or profile changed; sign in again.");
    }

    public static void ValidateKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 200 || key.Any(c => c < '!' || c > '~'))
            throw new ValidationException(
                "A single Idempotency-Key header containing 1–200 visible ASCII characters is required.",
                new Dictionary<string, string[]> { ["Idempotency-Key"] = ["A valid idempotency key is required."] });
    }

    public static string HashRequest(string operation, Guid? bookingId, object normalizedPayload)
    {
        var json = JsonSerializer.Serialize(new
            { Operation = operation, Target = bookingId, Payload = normalizedPayload });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}