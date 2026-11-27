using ShuttleVNBackend.Core.Entities.Courts;

namespace ShuttleVNBackend.Application.Interfaces.Repositories.Courts;

public interface IPricingRuleRepository
{
    Task<List<PricingRule>> GetByCourtAsync(int courtId, int? dayOfWeek, CancellationToken ct = default);

    Task<List<PricingRule>> ReplaceDayAsync(
        int courtId,
        int dayOfWeek,
        IReadOnlyCollection<PricingRule> pricingRules,
        CancellationToken ct = default);
}