using System.Globalization;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.Interfaces.Repositories.Courts;
using ShuttleVNBackend.Application.Interfaces.Users;
using ShuttleVNBackend.Application.UseCases.Bookings.Helpers;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.Courts.Enums;
using ShuttleVNBackend.Core.Entities.System.Enums;

namespace ShuttleVNBackend.Application.UseCases.Bookings.Services;

public class BookingRequestService(
    IBookingRepository bookingRepository,
    ICourtRepository courtRepository,
    IInvoiceCodeGenerator invoiceCodeGenerator,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock,
    IBookingIdempotency idempotency,
    BookingLifecycle lifecycle)
{
    public Task<BookingDetailDto> ConfirmAsync(Guid bookingId, ConfirmBookingDto dto, string key,
        CancellationToken ct = default)
    {
        return ExecuteMutationAsync(bookingId, key, "booking.confirm",
            NormalizePayment(dto), token => ConfirmCoreAsync(bookingId, dto, token), ct);
    }

    public Task<BookingDetailDto> RejectAsync(Guid bookingId, RejectBookingDto dto, string key,
        CancellationToken ct = default)
    {
        return ExecuteMutationAsync(bookingId, key, "booking.reject",
            new { Reason = dto.Reason?.Trim() }, token => RejectCoreAsync(bookingId, dto, token), ct);
    }

    public Task<BookingDetailDto> CompleteAsync(Guid bookingId, ConfirmBookingDto? dto, string key,
        CancellationToken ct = default)
    {
        return ExecuteMutationAsync(bookingId, key, "booking.complete",
            NormalizePayment(dto), token => CompleteCoreAsync(bookingId, dto, token), ct);
    }

    private async Task<BookingDetailDto> ExecuteMutationAsync(Guid bookingId, string key, string operation,
        object payload, Func<CancellationToken, Task<BookingMutationResponse<BookingDetailDto>>> action,
        CancellationToken ct)
    {
        RequireEmployeeId();
        await lifecycle.SettleAsync(bookingId, ct);
        var response = await idempotency.ExecuteAsync(
            idempotency.GetScope(), key, operation, bookingId, payload,
            token => AuthorizeMutationAsync(bookingId, token), action, ct);

        return response.GetData();
    }

    private async Task AuthorizeMutationAsync(Guid bookingId, CancellationToken ct)
    {
        RequireEmployeeId();
        await lifecycle.LockAsync(bookingId, ct: ct);
    }

    private static object NormalizePayment(ConfirmBookingDto? dto)
    {
        return new
        {
            HasPayment = dto is not null,
            dto?.IsPaid,
            Amount = dto?.Amount?.ToString("G29", CultureInfo.InvariantCulture),
            dto?.PaymentMethod,
            dto?.PaidAt,
            Note = dto?.Note?.Trim() ?? string.Empty
        };
    }

    public async Task SettleEndedBookingsAsync(CancellationToken ct = default)
    {
        var localCutoff = VietnamTime.NowLocal(clock);
        var bookingIds = await bookingRepository.GetSettlementCandidateIdsAsync(localCutoff, ct);
        foreach (var bookingId in bookingIds)
            await unitOfWork.ExecuteInTransactionAsync(token => SettleBookingAsync(bookingId, token), ct);
    }

    private async Task<bool> SettleBookingAsync(Guid bookingId, CancellationToken ct)
    {
        var booking = await lifecycle.LockAsync(bookingId, ct: ct);
        return await lifecycle.SettleLockedAsync(booking, ct);
    }

    public async Task<PagedResult<BookingListItemDto>> GetPendingAsync(PageRequest page, CancellationToken ct = default)
    {
        var rows = await bookingRepository.GetPendingAsync(page, ct);
        return new PagedResult<BookingListItemDto>
        {
            Items = rows.Items.Select(r => r.ToListItem()).ToList(),
            PageNumber = rows.PageNumber,
            PageSize = rows.PageSize,
            TotalCount = rows.TotalCount
        };
    }

    public async Task<BookingDetailDto> GetDetailAsync(Guid bookingId, CancellationToken ct = default)
    {
        var row = await bookingRepository.GetRowByIdAsync(bookingId, ct)
                  ?? throw new NotFoundException("Booking not found");

        var invoice = await bookingRepository.GetValidInvoiceAsync(bookingId, ct);
        var history = await bookingRepository.GetStatusHistoryAsync(bookingId, ct);
        return row.ToDetail(invoice, history);
    }

    private async Task<BookingMutationResponse<BookingDetailDto>> ConfirmCoreAsync(Guid bookingId,
        ConfirmBookingDto dto,
        CancellationToken ct = default)
    {
        var employeeId = RequireEmployeeId();

        var conflict = await unitOfWork.ExecuteInTransactionAsync<string?>(
            token => ConfirmBookingAsync(bookingId, dto, employeeId, token), ct);

        return await FinishAsync(bookingId, conflict, ct);
    }

    private async Task<string?> ConfirmBookingAsync(Guid bookingId, ConfirmBookingDto dto, Guid employeeId,
        CancellationToken ct)
    {
        var booking = await lifecycle.LockAsync(bookingId, ct: ct);
        if (await lifecycle.SettleLockedAsync(booking, ct))
            return "Booking end time passed; booking was settled automatically. Reload its current state.";

        ValidatePayment(dto, booking.TotalCost);

        if (booking.Status == BookingStatus.Confirmed) return null;

        if (booking.Status != BookingStatus.Pending)
            throw new ConflictException($"The booking is already {booking.Status}.");

        if (HasStarted(booking))
            throw new ConflictException(
                "The booking has already started; confirmation is no longer allowed.");

        await EnsureCourtAvailableAsync(booking, ct);

        var now = clock.GetUtcNow().UtcDateTime;
        await ChangeStatusAsync(booking, BookingStatus.Confirmed, employeeId, "Confirmed by staff", now, ct);
        await IssueInvoiceAsync(booking, dto, employeeId, now, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return null;
    }

    private async Task<BookingMutationResponse<BookingDetailDto>> RejectCoreAsync(Guid bookingId, RejectBookingDto dto,
        CancellationToken ct = default)
    {
        var reason = dto.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new ValidationException(errors: new Dictionary<string, string[]>
            {
                ["Reason"] = ["Reason is required"]
            });

        var employeeId = RequireEmployeeId();
        var conflict = await unitOfWork.ExecuteInTransactionAsync<string?>(
            token => RejectBookingAsync(bookingId, reason, employeeId, token), ct);

        return await FinishAsync(bookingId, conflict, ct);
    }

    private async Task<string?> RejectBookingAsync(Guid bookingId, string reason, Guid employeeId, CancellationToken ct)
    {
        var booking = await lifecycle.LockAsync(bookingId, ct: ct);
        if (await lifecycle.SettleLockedAsync(booking, ct))
            return "Booking end time passed; booking was settled automatically. Reload its current state.";

        if (booking.Status == BookingStatus.Cancelled) return null;

        if (booking.Status == BookingStatus.Confirmed)
            throw new ConflictException(
                "Confirmed bookings cannot be rejected; use the cancellation flow instead.");

        if (booking.Status != BookingStatus.Pending)
            throw new ConflictException($"The booking is already {booking.Status}.");

        var now = clock.GetUtcNow().UtcDateTime;
        await ChangeStatusAsync(booking, BookingStatus.Cancelled, employeeId, reason, now, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return null;
    }

    private async Task<BookingMutationResponse<BookingDetailDto>> CompleteCoreAsync(Guid bookingId,
        ConfirmBookingDto? dto,
        CancellationToken ct = default)
    {
        RequireEmployeeId();

        var conflict = await unitOfWork.ExecuteInTransactionAsync<string?>(
            token => CompleteBookingAsync(bookingId, token), ct);

        return await FinishAsync(bookingId, conflict, ct);
    }

    private async Task<string?> CompleteBookingAsync(Guid bookingId, CancellationToken ct)
    {
        var booking = await lifecycle.LockAsync(bookingId, ct: ct);
        if (await lifecycle.SettleLockedAsync(booking, ct))
            return "Booking end time passed; booking was settled automatically. Reload its current state.";

        if (booking.Status == BookingStatus.Completed) return null;

        if (booking.Status == BookingStatus.Cancelled) throw new ConflictException("The booking is already Cancelled.");

        throw new ConflictException(
            "The booking has not ended yet; manual completion cannot bypass automatic settlement.");
    }

    private async Task<BookingMutationResponse<BookingDetailDto>> FinishAsync(Guid bookingId, string? conflict,
        CancellationToken ct)
    {
        return conflict is not null
            ? new BookingMutationResponse<BookingDetailDto>(null, conflict, bookingId)
            : new BookingMutationResponse<BookingDetailDto>(await GetDetailAsync(bookingId, ct), BookingId: bookingId);
    }

    private Guid RequireEmployeeId()
    {
        return currentUser is { ActorType: ActorType.Employee, ActorId: { } id }
            ? id
            : throw new UnauthorizedException("Employee identity is required.");
    }

    private bool HasStarted(Booking booking)
    {
        return VietnamTime.NowLocal(clock) >= VietnamTime.ToLocalDateTime(booking.Date, booking.StartTime);
    }

    private async Task ChangeStatusAsync(Booking booking, BookingStatus newStatus, Guid? employeeId, string reason,
        DateTime now, CancellationToken ct, ActorType actor = ActorType.Employee)
    {
        var oldStatus = booking.Status;
        booking.Status = newStatus;
        booking.UpdatedAt = now;

        await unitOfWork.AddAsync(new BookingStatusHistory
        {
            Id = Guid.NewGuid(),
            BookingId = booking.BookingId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ActorType = actor,
            ChangedByEmployee = actor == ActorType.Employee ? employeeId : null,
            ChangedAt = now,
            Reason = reason
        }, ct);
    }

    private async Task IssueInvoiceAsync(Booking booking, ConfirmBookingDto dto, Guid employeeId, DateTime now,
        CancellationToken ct)
    {
        var code = await invoiceCodeGenerator.NextAsync(VietnamTime.TodayLocal(clock), ct);
        await unitOfWork.AddAsync(new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            InvoiceCode = code,
            BookingId = booking.BookingId,
            TotalCost = booking.TotalCost,
            Status = dto.IsPaid ? InvoiceStatus.Paid : InvoiceStatus.Unpaid,
            IssuedByEmployee = employeeId,
            IssuedAt = now,
            PaidByEmployee = dto.IsPaid ? employeeId : null,
            PaidAt = dto.IsPaid ? dto.PaidAt : null,
            PaidAmount = dto.IsPaid ? dto.Amount : null,
            PaymentMethod = dto.IsPaid ? dto.PaymentMethod : null,
            Note = dto.Note?.Trim() ?? string.Empty
        }, ct);
    }

    private async Task EnsureCourtAvailableAsync(Booking booking, CancellationToken ct)
    {
        var court = await courtRepository.GetByIdAsync(booking.CourtId, ct)
                    ?? throw new ConflictException("The court no longer exists.");
        if (court.Status != CourtStatus.Active)
            throw new ConflictException("The court is not active.");

        var schedules = await courtRepository.GetSchedulesAsync(booking.CourtId, ct) ?? [];
        var isoDay = booking.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)booking.Date.DayOfWeek;
        var isOpen = schedules.Any(s => s.DayOfWeek == isoDay && s.IsAvailable
                                                              && s.OpenTime <= booking.StartTime &&
                                                              booking.EndTime <= s.CloseTime);
        if (!isOpen)
            throw new ConflictException("The court is not available at the requested time.");

        if (await bookingRepository.HasOverlapAsync(booking.CourtId, booking.Date, booking.StartTime,
                booking.EndTime, booking.BookingId, ct))
            throw new ConflictException("The time slot overlaps with another booking.");
    }

    private void ValidatePayment(ConfirmBookingDto dto, decimal total)
    {
        if (dto.IsPaid)
        {
            if (dto.Amount is null || dto.PaymentMethod is null || dto.PaidAt is null)
                throw new ValidationException("Payment amount, method and actual UTC payment time are required.");

            InvoiceRules.ValidatePayment(dto.Amount.Value, total, dto.PaymentMethod.Value,
                dto.PaidAt.Value, clock.GetUtcNow().UtcDateTime);
        }
        else if (dto.Amount is not null || dto.PaymentMethod is not null || dto.PaidAt is not null)
        {
            throw new ValidationException(errors: new Dictionary<string, string[]>
            {
                ["IsPaid"] = ["Payment amount, method and time must be empty when the booking is unpaid"]
            });
        }
    }
}