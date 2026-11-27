using ShuttleVNBackend.Application.DTOs.Bookings;

namespace ShuttleVNBackend.Application.Exceptions;

public sealed class PriceChangedException(PricePreviewResponse quote)
    : ConflictException("Price has changed. Please confirm the new quote.")
{
    public PricePreviewResponse Quote { get; } = quote;
}