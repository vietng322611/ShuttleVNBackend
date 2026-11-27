using ShuttleVNBackend.Application.Common;

namespace ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;

public interface IBookingIdempotency
{
    string GetScope(bool guest = false, bool requireEstablished = false);

    Task LockVerificationAsync(string normalizedEmail, CancellationToken ct = default);

    Task<BookingMutationResponse<T>> ExecuteAsync<T>(
        string scope, string key, string operation, Guid? bookingId, object payload,
        Func<CancellationToken, Task> authorize,
        Func<CancellationToken, Task<BookingMutationResponse<T>>> action,
        CancellationToken ct = default);
}