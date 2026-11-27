using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Courts;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.Interfaces.Repositories.Courts;
using ShuttleVNBackend.Application.Interfaces.Users;
using ShuttleVNBackend.Application.UseCases.Courts.Helpers;
using ShuttleVNBackend.Core.Entities.Courts;
using ShuttleVNBackend.Core.Entities.Courts.Enums;
using ShuttleVNBackend.Core.Entities.System.Enums;

namespace ShuttleVNBackend.Application.UseCases.Courts.Services;

public class CourtService(
    ICourtRepository courtRepository,
    IBookingRepository bookingRepository,
    ICurrentUser currentUser,
    TimeProvider clock,
    IUnitOfWork unitOfWork)
{
    private const int SlotMinutes = 30;
    private const int SlotCount = 34;
    private static readonly TimeOnly GridStart = new(5, 0);

    public async Task<PagedResult<CourtDto>> GetCourtsAsync(CancellationToken ct = default)
    {
        var courts = await courtRepository.GetAllAsync(ct);
        var inUse = await GetInUseIdsAsync(ct);
        var dtos = courts.Select(c => ToDto(c, inUse)).ToList();

        return new PagedResult<CourtDto>
        {
            Items = dtos,
            PageNumber = 1,
            PageSize = dtos.Count,
            TotalCount = dtos.Count
        };
    }

    public async Task<CourtDto?> GetCourtByIdAsync(int courtId, CancellationToken ct = default)
    {
        var court = await courtRepository.GetByIdAsync(courtId, ct);
        return court is null ? null : ToDto(court, await GetInUseIdsAsync(ct));
    }

    public async Task<CourtGridResponseDto> GetCourtGridAsync(DateOnly? date, CancellationToken ct = default)
    {
        var today = VietnamTime.TodayLocal(clock);
        var day = date ?? today;
        var isoDay = day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek;

        var sources = await courtRepository.GetAllForGridAsync(day, isoDay, ct);
        var inUse = day == today ? await GetInUseIdsAsync(ct) : [];
        var viewerCustomerId = currentUser.ActorType == ActorType.Customer ? currentUser.ActorId : null;

        var items = sources.Select(s => new CourtGridItemDto(
            ToDto(s.Court, inUse),
            Enumerable.Range(0, SlotCount)
                .Select(i => BuildSlot(s, day, GridStart.AddMinutes(i * SlotMinutes), viewerCustomerId))
                .ToList())).ToList();

        return new CourtGridResponseDto(day, items);
    }

    public Task<UpdateCourtStatusResultDto> UpdateCourtStatusAsync(
        int courtId,
        UpdateCourtStatusDto dto,
        CancellationToken ct = default)
    {
        return unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.LockCourtAsync(courtId, token);
            if (!Enum.IsDefined(dto.Status))
                throw new ValidationException(errors: new Dictionary<string, string[]>
                { ["Status"] = ["Invalid court status"] });

            var court = await courtRepository.GetByIdAsync(courtId, token)
                        ?? throw new NotFoundException("Court not found");

            var oldStatus = court.Status;
            if (oldStatus != dto.Status)
            {
                court.Status = dto.Status;
                court.UpdatedAt = clock.GetUtcNow().UtcDateTime;

                await unitOfWork.SaveChangesAsync(token);
            }

            var affected = dto.Status == CourtStatus.Active
                ? 0
                : await bookingRepository.CountUpcomingByCourtAsync(courtId,
                    VietnamTime.TodayLocal(clock), token);

            return new UpdateCourtStatusResultDto(ToDto(court, await GetInUseIdsAsync(token)), affected);
        }, ct);
    }

    private async Task<HashSet<int>> GetInUseIdsAsync(CancellationToken ct)
    {
        var now = VietnamTime.NowLocal(clock);
        return await courtRepository.GetCourtIdsInUseAsync(
            DateOnly.FromDateTime(now), TimeOnly.FromDateTime(now), ct);
    }

    private static CourtDto ToDto(BadmintonCourt c, HashSet<int> inUse)
    {
        return CourtDto.FromEntity(c, c.Status == CourtStatus.Active && inUse.Contains(c.CourtId));
    }

    private static CourtSlotDto BuildSlot(
        CourtGridSource s, DateOnly date, TimeOnly start, Guid? viewerCustomerId)
    {
        var end = start.AddMinutes(SlotMinutes);
        var court = s.Court;

        var isOpen = court.Status == CourtStatus.Active
                     && court.CourtSchedules.Any(x => x.IsAvailable && x.OpenTime <= start && end <= x.CloseTime);

        var booking = s.Bookings.FirstOrDefault(b => b.StartTime < end && b.EndTime > start);

        var status = !isOpen ? "CLOSED" : booking is not null ? "BOOKED" : "AVAILABLE";
        var isMine = booking is not null && viewerCustomerId is not null
                     && booking.CustomerId == viewerCustomerId;
        return new CourtSlotDto(
            court.CourtId, date, start.ToString("HH:mm"), end.ToString("HH:mm"), status,
            status == "BOOKED" ? booking!.BookingId : null,
            ResolvePricePerHour(court.PricingRules, start, end),
            isMine);
    }

    private static decimal ResolvePricePerHour(IEnumerable<PricingRule> rules, TimeOnly start, TimeOnly end)
    {
        var orderedRules = rules.OrderBy(r => r.StartTime).ToList();
        decimal total = 0;
        var covered = 0;

        for (var index = 0; index < orderedRules.Count; index++)
        {
            var rule = orderedRules[index];
            var ruleEnd = index + 1 < orderedRules.Count ? orderedRules[index + 1].StartTime : TimeOnly.MaxValue;
            var from = start > rule.StartTime ? start : rule.StartTime;
            var to = end < ruleEnd ? end : ruleEnd;
            if (from >= to)
            {
                continue;
            }

            var minutes = (int)(to - from).TotalMinutes;
            total += rule.PricePerHour * minutes;
            covered += minutes;
        }

        return covered == 0 ? 0 : Math.Round(total / covered, 2);
    }

    public async Task<IReadOnlyList<BadmintonCourt>> GetAllCourtsAsync(CancellationToken ct = default)
    {
        return await courtRepository.GetAllAsync(ct);
    }

    public async Task<BadmintonCourt> CreateCourtAsync(CreateCourtDto request, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();

        var name = request.Name.Trim();
        if (name.Length == 0)
            errors["Name"] = ["Court name is required."];
        CourtValidation.ValidateTimeRange(
            request.DefaultOpenTime,
            request.DefaultCloseTime,
            "DefaultCloseTime",
            errors);
        if (request.DefaultPricePerHour <= 0)
            errors["DefaultPricePerHour"] = ["DefaultPricePerHour must be greater than 0."];
        CourtValidation.ThrowIfAny(errors);

        if (await courtRepository.NameExistsAsync(name, null, ct))
            throw new ConflictException($"Court with name '{name}' already exists.");

        var now = clock.GetUtcNow().UtcDateTime;
        var court = new BadmintonCourt
        {
            Name = name,
            Description = request.Description?.Trim() ?? string.Empty,
            Status = CourtStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            CourtSchedules = [],
            PricingRules = []
        };

        for (var day = 1; day < 8; day++)
        {
            court.CourtSchedules.Add(new CourtSchedule
            {
                DayOfWeek = day,
                OpenTime = request.DefaultOpenTime,
                CloseTime = request.DefaultCloseTime,
                IsAvailable = true,
                CreatedAt = now,
                UpdatedAt = now
            });

            court.PricingRules.Add(new PricingRule
            {
                DayOfWeek = day,
                StartTime = request.DefaultOpenTime,
                PricePerHour = request.DefaultPricePerHour,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await unitOfWork.AddAsync(court, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return court;
    }

    public Task<BadmintonCourt> UpdateCourtAsync(
        int courtId,
        UpdateCourtDto request,
        CancellationToken ct = default)
    {
        return unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.LockCourtAsync(courtId, token);
            var name = request.Name.Trim();
            if (name.Length == 0)
                throw new ValidationException(errors: new Dictionary<string, string[]>
                {
                    ["Name"] = ["Court name is required."]
                });

            var court = await GetCourtOrThrowAsync(courtId, token);

            if (await courtRepository.NameExistsAsync(name, courtId, token))
                throw new ConflictException($"Court with name '{name}' already exists.");

            court.Name = name;
            court.Description = request.Description?.Trim() ?? string.Empty;
            court.UpdatedAt = clock.GetUtcNow().UtcDateTime;

            await unitOfWork.SaveChangesAsync(token);
            return court;
        }, ct);
    }

    public Task SetCourtStatusAsync(
        int courtId,
        CourtStatus status,
        CancellationToken ct = default)
    {
        return unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            await unitOfWork.LockCourtAsync(courtId, token);
            var court = await GetCourtOrThrowAsync(courtId, token);

            court.Status = status;
            court.UpdatedAt = clock.GetUtcNow().UtcDateTime;

            return await unitOfWork.SaveChangesAsync(token);
        }, ct);
    }

    private async Task<BadmintonCourt> GetCourtOrThrowAsync(int courtId, CancellationToken ct)
    {
        return await courtRepository.GetByIdAsync(courtId, ct)
               ?? throw new NotFoundException($"Court with id {courtId} not found.");
    }
}