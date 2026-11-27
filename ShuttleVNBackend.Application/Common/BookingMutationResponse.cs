using ShuttleVNBackend.Application.Exceptions;

namespace ShuttleVNBackend.Application.Common;

public sealed record BookingMutationResponse<T>(
    T? Data,
    string? Conflict = null,
    Guid? BookingId = null,
    Dictionary<string, string[]>? ValidationErrors = null)
{
    public T GetData()
    {
        if (ValidationErrors is not null)
            throw new ValidationException(Conflict ?? "Validation failed", ValidationErrors);

        if (Conflict is not null)
            throw new ConflictException(Conflict);

        return Data ?? throw new InvalidOperationException("Booking mutation returned no response.");
    }
}