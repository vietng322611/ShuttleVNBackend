using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Application.DTOs.Bookings;

public record BookingSearchCriteria(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    int? CourtId = null,
    BookingStatus? Status = null,
    Guid? CustomerId = null,
    string? Keyword = null);