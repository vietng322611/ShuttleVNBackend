using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Application.DTOs.Statistics;

public record CourtUsageRow(int CourtId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime, BookingStatus Status);