using System.Security.Cryptography;
using System.Text;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.Exceptions;

namespace ShuttleVNBackend.Application.UseCases.Bookings.Services;

public static class BookingQuote
{
    public static PricePreviewResponse Create(
        int courtId,
        DateOnly date,
        TimeOnly startTime,
        TimeOnly endTime,
        decimal totalCost,
        IReadOnlyList<BookingPriceSegmentDto> segments)
    {
        var orderedSegments = segments.OrderBy(s => s.SegmentNo)
            .ThenBy(s => s.PriceStartTime)
            .ThenBy(s => s.SegmentStartTime)
            .ThenBy(s => s.SegmentEndTime)
            .ThenBy(s => s.PricePerHour)
            .ToArray();
        var canonical = new StringBuilder(FormattableString.Invariant(
            $"{courtId}|{date:yyyy-MM-dd}|{startTime:HH:mm:ss.fffffff}|{endTime:HH:mm:ss.fffffff}|{totalCost:G29}|{orderedSegments.Length}"));
        foreach (var segment in orderedSegments)
            canonical.Append(FormattableString.Invariant(
                $"\n{segment.SegmentNo}|{segment.PriceStartTime:HH:mm:ss.fffffff}|{segment.SegmentStartTime:HH:mm:ss.fffffff}|{segment.SegmentEndTime:HH:mm:ss.fffffff}|{segment.PricePerHour:G29}"));

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
        return new PricePreviewResponse(totalCost, orderedSegments, fingerprint);
    }

    public static void ValidateConfirmation(CreateBookingDto dto, PricePreviewResponse currentQuote)
    {
        var errors = new Dictionary<string, string[]>();
        if (dto.TotalCost is null)
            errors[nameof(dto.TotalCost)] = ["Total cost confirmation is required."];
        else if (dto.TotalCost < 0)
            errors[nameof(dto.TotalCost)] = ["Total cost must not be negative."];
        if (string.IsNullOrWhiteSpace(dto.QuoteFingerprint))
            errors[nameof(dto.QuoteFingerprint)] = ["Quote fingerprint confirmation is required."];

        if (errors.Count > 0)
            throw new ValidationException("Price confirmation is required.", errors);

        if (dto.TotalCost != currentQuote.TotalCost ||
            !string.Equals(dto.QuoteFingerprint, currentQuote.QuoteFingerprint, StringComparison.Ordinal))
            throw new PriceChangedException(currentQuote);
    }
}