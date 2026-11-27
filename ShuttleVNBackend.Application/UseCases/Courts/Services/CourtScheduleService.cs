using ShuttleVNBackend.Application.DTOs.Courts;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Courts;
using ShuttleVNBackend.Application.UseCases.Courts.Helpers;
using ShuttleVNBackend.Core.Entities.Courts;

namespace ShuttleVNBackend.Application.UseCases.Courts.Services;

public class CourtScheduleService(
    ICourtRepository courtRepository,
    IPricingRuleRepository pricingRuleRepository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<CourtSchedule>> GetSchedulesAsync(int courtId, CancellationToken ct = default)
    {
        var schedules = await courtRepository.GetSchedulesAsync(courtId, ct);
        return schedules == null
            ? throw new NotFoundException($"Court with id {courtId} not found.")
            : schedules.OrderBy(s => s.DayOfWeek).ToList();
    }

    public Task<CourtSchedule> UpdateScheduleAsync(
        int courtId,
        int dayOfWeek,
        UpdateCourtScheduleDto request,
        CancellationToken ct = default)
    {
        return unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.LockCourtAsync(courtId, token);
            var errors = new Dictionary<string, string[]>();
            CourtValidation.ValidateDayOfWeek(dayOfWeek, errors);
            if (request.IsAvailable)
                CourtValidation.ValidateTimeRange(request.OpenTime, request.CloseTime, "CloseTime", errors);
            CourtValidation.ThrowIfAny(errors);

            var schedules = await courtRepository.GetSchedulesAsync(courtId, token);
            if (schedules == null)
                throw new NotFoundException($"Court with id {courtId} not found.");

            var schedule = schedules.FirstOrDefault(s => s.DayOfWeek == dayOfWeek)
                           ?? throw new NotFoundException(
                               $"Schedule for day {dayOfWeek} of court {courtId} not found.");

            var now = clock.GetUtcNow().UtcDateTime;
            var changed = schedule.IsAvailable != request.IsAvailable
                          || schedule.OpenTime != request.OpenTime
                          || schedule.CloseTime != request.CloseTime;

            schedule.OpenTime = request.OpenTime;
            schedule.CloseTime = request.CloseTime;
            schedule.IsAvailable = request.IsAvailable;
            schedule.UpdatedAt = now;

            if (changed)
            {
                var rules = await pricingRuleRepository.GetByCourtAsync(courtId, dayOfWeek, token);
                var removed = PricingRuleNormalizer.Normalize(rules, request.OpenTime, request.CloseTime, now);
                unitOfWork.RemoveRange(removed);
            }

            await unitOfWork.SaveChangesAsync(token);
            return schedule;
        }, ct);
    }
}