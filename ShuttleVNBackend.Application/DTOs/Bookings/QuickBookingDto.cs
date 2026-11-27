using System.ComponentModel.DataAnnotations;

namespace ShuttleVNBackend.Application.DTOs.Bookings;

public sealed record QuickBookingDto(
    string FullName,
    string Phone,
    string Email,
    int CourtId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    [Required] decimal? TotalCost,
    string VerificationCode,
    string? QuoteFingerprint = null
) : CreateBookingDto(
    CourtId,
    Date,
    StartTime,
    EndTime,
    TotalCost,
    QuoteFingerprint
);