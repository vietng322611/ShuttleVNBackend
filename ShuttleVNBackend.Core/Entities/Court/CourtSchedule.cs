namespace ShuttleVNBackend.Core.Entities.Court;

public class CourtSchedule
{
    public int ScheduleId { get; set; }
    public int CourtId { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime UpdatedAt { get; set; }
}