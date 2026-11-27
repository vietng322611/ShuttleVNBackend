using System.ComponentModel.DataAnnotations;

namespace ShuttleVNBackend.Application.DTOs.Bookings;

public record CreateBookingDto(
    int CourtId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    [Required] decimal? TotalCost,
    string? QuoteFingerprint = null
);