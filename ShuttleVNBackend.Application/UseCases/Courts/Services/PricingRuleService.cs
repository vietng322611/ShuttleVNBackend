using ShuttleVNBackend.Application.DTOs.Courts;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Courts;
using ShuttleVNBackend.Application.UseCases.Courts.Helpers;
using ShuttleVNBackend.Core.Entities.Courts;

namespace ShuttleVNBackend.Application.UseCases.Courts.Services;

public class PricingRuleService(
    IPricingRuleRepository pricingRuleRepository,
    ICourtRepository courtRepository,
    TimeProvider clock,
    IUnitOfWork unitOfWork)
{
    public async Task<IReadOnlyList<PricingRule>> GetPricingRulesAsync(int courtId, CancellationToken ct = default)
    {
        var rules = await pricingRuleRepository.GetByCourtAsync(courtId, null, ct);
        return rules == null
            ? throw new NotFoundException($"Court with id {courtId} not found.")
            : rules.OrderBy(r => r.DayOfWeek).ThenBy(r => r.StartTime).ToList();
    }

    public Task<IReadOnlyList<PricingRule>> SavePricingRulesAsync(
        int courtId,
        int dayOfWeek,
        SavePricingRuleDto request,
        CancellationToken ct = default)
    {
        return unitOfWork.ExecuteInTransactionAsync<IReadOnlyList<PricingRule>>(async token =>
        {
            await unitOfWork.LockCourtAsync(courtId, token);
            var errors = new Dictionary<string, string[]>();
            CourtValidation.ValidateDayOfWeek(dayOfWeek, errors);

            var schedules = await courtRepository.GetSchedulesAsync(courtId, token);
            if (schedules is null)
                throw new InvalidOperationException($"Court with id {courtId} not found.");

            var schedule = schedules.FirstOrDefault(s => s.DayOfWeek == dayOfWeek && s.IsAvailable);
            if (schedule is null)
                throw new InvalidOperationException($"Court {courtId} has no schedule for day {dayOfWeek}.");

            ValidateRules(request.PricingRules, schedule, errors);
            CourtValidation.ThrowIfAny(errors);

            var now = clock.GetUtcNow().UtcDateTime;
            var rules = request.PricingRules
                .OrderBy(r => r.StartTime)
                .Select(r => new PricingRule
                {
                    CourtId = courtId,
                    DayOfWeek = dayOfWeek,
                    StartTime = r.StartTime,
                    PricePerHour = r.PricePerHour,
                    CreatedAt = now,
                    UpdatedAt = now
                })
                .ToList();

            return await pricingRuleRepository.ReplaceDayAsync(courtId, dayOfWeek, rules, token);
        }, ct);
    }

    private static void ValidateRules(
        List<SavePricingRuleItemDto> rules,
        CourtSchedule schedule,
        Dictionary<string, string[]> errors)
    {
        if (rules.Count == 0)
        {
            errors["PricingRules"] = ["At least one pricing rule is required."];
            return;
        }

        var ordered = rules.OrderBy(r => r.StartTime).ToList();
        if (ordered[0].StartTime != schedule.OpenTime)
            errors["PricingRules"] = [$"The first pricing rule must start at {schedule.OpenTime}."];
        else if (ordered.Any(r => r.StartTime < schedule.OpenTime || r.StartTime >= schedule.CloseTime))
            errors["PricingRules"] =
                [$"Pricing rule start times must be within {schedule.OpenTime}-{schedule.CloseTime}."];
        else if (ordered.Select(r => r.StartTime).Distinct().Count() != ordered.Count)
            errors["PricingRules"] = ["Pricing rule start times must be unique."];
        if (ordered.Any(r => r.PricePerHour <= 0))
            errors["PricePerHour"] = ["PricePerHour must be greater than 0."];
    }
}