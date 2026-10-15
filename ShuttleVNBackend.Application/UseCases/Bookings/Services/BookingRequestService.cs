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
    TimeProvider clock)
{
    private static readonly TimeSpan PaidAtClockSkew = TimeSpan.FromMinutes(1);

    private const string ExpiredReason =
        "Automatically cancelled: the usage date passed before the request was processed.";

    private const string ExpiredMessage =
        "The usage date has passed; the pending request was cancelled automatically.";

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

    public async Task<BookingDetailDto> ConfirmAsync(Guid bookingId, ConfirmBookingDto dto,
        CancellationToken ct = default)
    {
        ValidatePayment(dto);
        var employeeId = RequireEmployeeId();

        var conflict = await unitOfWork.ExecuteInTransactionAsync<string?>(async token =>
        {
            var booking = await LockAsync(bookingId, token);
            if (booking.Status == BookingStatus.Confirmed) return null;
            if (booking.Status != BookingStatus.Pending)
                throw new ConflictException($"The booking is already {booking.Status}.");

            if (IsPastUsageDate(booking)) return await ExpireAsync(booking, token);
            if (HasStarted(booking))
                throw new ConflictException(
                    "The booking has already started; record usage or cancel it instead.");

            await EnsureCourtAvailableAsync(booking, token);

            var now = clock.GetUtcNow().UtcDateTime;
            await ChangeStatusAsync(booking, BookingStatus.Confirmed, employeeId, "Confirmed by staff", now, token);
            await IssueInvoiceAsync(booking, dto, employeeId, now, token);
            await unitOfWork.SaveChangesAsync(token);
            return null;
        }, ct);

        return await FinishAsync(bookingId, conflict, ct);
    }

    public async Task<BookingDetailDto> RejectAsync(Guid bookingId, RejectBookingDto dto,
        CancellationToken ct = default)
    {
        var reason = dto.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new ValidationException(errors: new Dictionary<string, string[]>
                { ["Reason"] = ["Reason is required"] });

        var employeeId = RequireEmployeeId();

        var conflict = await unitOfWork.ExecuteInTransactionAsync<string?>(async token =>
        {
            var booking = await LockAsync(bookingId, token);
            if (booking.Status == BookingStatus.Cancelled) return null;
            if (booking.Status == BookingStatus.Confirmed)
                throw new ConflictException(
                    "Confirmed bookings cannot be rejected; use the cancellation flow instead.");
            if (booking.Status != BookingStatus.Pending)
                throw new ConflictException($"The booking is already {booking.Status}.");

            if (IsPastUsageDate(booking)) return await ExpireAsync(booking, token);

            var now = clock.GetUtcNow().UtcDateTime;
            await ChangeStatusAsync(booking, BookingStatus.Cancelled, employeeId, reason, now, token);
            await unitOfWork.SaveChangesAsync(token);
            return null;
        }, ct);

        return await FinishAsync(bookingId, conflict, ct);
    }

    public async Task<BookingDetailDto> CompleteAsync(Guid bookingId, ConfirmBookingDto? dto,
        CancellationToken ct = default)
    {
        var employeeId = RequireEmployeeId();

        var conflict = await unitOfWork.ExecuteInTransactionAsync<string?>(async token =>
        {
            var booking = await LockAsync(bookingId, token);
            if (booking.Status == BookingStatus.Completed) return null;
            if (booking.Status == BookingStatus.Cancelled)
                throw new ConflictException("The booking is already Cancelled.");

            if (booking.Status == BookingStatus.Pending && IsPastUsageDate(booking))
                return await ExpireAsync(booking, token);
            if (!HasEnded(booking))
                throw new ConflictException("The booking has not ended yet.");

            var now = clock.GetUtcNow().UtcDateTime;
            if (booking.Status == BookingStatus.Pending)
            {
                if (dto is null)
                    throw new ValidationException("Payment information is required to complete a pending booking.");
                ValidatePayment(dto);
                await IssueInvoiceAsync(booking, dto, employeeId, now, token);
            }

            await ChangeStatusAsync(booking, BookingStatus.Completed, employeeId, "Usage recorded by staff", now,
                token);
            await unitOfWork.SaveChangesAsync(token);
            return null;
        }, ct);

        return await FinishAsync(bookingId, conflict, ct);
    }

    private async Task<BookingDetailDto> FinishAsync(Guid bookingId, string? conflict, CancellationToken ct)
    {
        if (conflict is not null) throw new ConflictException(conflict);
        return await GetDetailAsync(bookingId, ct);
    }

    private async Task<Booking> LockAsync(Guid bookingId, CancellationToken ct)
    {
        return await bookingRepository.GetByIdForUpdateAsync(bookingId, ct)
               ?? throw new NotFoundException("Booking not found");
    }

    private Guid RequireEmployeeId()
    {
        return currentUser is { ActorType: ActorType.Employee, ActorId: { } id }
            ? id
            : throw new UnauthorizedException("Employee identity is required.");
    }

    private bool IsPastUsageDate(Booking booking)
    {
        return booking.Date < VietnamTime.TodayLocal(clock);
    }

    private bool HasStarted(Booking booking)
    {
        return VietnamTime.NowLocal(clock) >= VietnamTime.ToLocalDateTime(booking.Date, booking.StartTime);
    }

    private bool HasEnded(Booking booking)
    {
        return VietnamTime.NowLocal(clock) >= VietnamTime.ToLocalDateTime(booking.Date, booking.EndTime);
    }

    private async Task<string> ExpireAsync(Booking booking, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await ChangeStatusAsync(booking, BookingStatus.Cancelled, null, ExpiredReason, now, ct,
            ActorType.System);
        await unitOfWork.SaveChangesAsync(ct);
        return ExpiredMessage;
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
            PaidAt = dto.IsPaid ? NormalizeUtc(dto.PaidAt!.Value) : null,
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
                                        && s.OpenTime <= booking.StartTime && booking.EndTime <= s.CloseTime);
        if (!isOpen)
            throw new ConflictException("The court is not available at the requested time.");

        if (await bookingRepository.HasOverlapAsync(booking.CourtId, booking.Date, booking.StartTime,
                booking.EndTime, booking.BookingId, ct))
            throw new ConflictException("The time slot overlaps with another booking.");
    }

    private void ValidatePayment(ConfirmBookingDto dto)
    {
        var errors = new Dictionary<string, string[]>();
        if (dto.IsPaid)
        {
            if (dto.PaymentMethod is null || !Enum.IsDefined(dto.PaymentMethod.Value))
                errors["PaymentMethod"] = ["Payment method is required when the booking is paid"];
            if (dto.PaidAt is null)
                errors["PaidAt"] = ["Payment time is required when the booking is paid"];
            else if (NormalizeUtc(dto.PaidAt.Value) > clock.GetUtcNow().UtcDateTime + PaidAtClockSkew)
                errors["PaidAt"] = ["Payment time cannot be in the future"];
        }
        else if (dto.PaymentMethod is not null || dto.PaidAt is not null)
        {
            errors["IsPaid"] = ["Payment method and time must be empty when the booking is unpaid"];
        }

        if (errors.Count > 0) throw new ValidationException(errors: errors);
    }

    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}