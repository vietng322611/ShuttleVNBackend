using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.Interfaces.Users;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.System.Enums;

namespace ShuttleVNBackend.Application.UseCases.Bookings.Services;

public sealed class BookingLifecycle(
    IBookingRepository bookingRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public void Authorize(Booking booking)
    {
        if (currentUser.ActorId is null || currentUser.ActorType is not (ActorType.Customer or ActorType.Employee))
        {
            throw new UnauthorizedException();
        }
        if (currentUser.ActorType == ActorType.Customer && booking.CustomerId != currentUser.ActorId)
        {
            throw new NotFoundException("Booking not found.");
        }
    }

    public async Task<Booking> LockAsync(Guid bookingId, int? targetCourtId = null, CancellationToken ct = default)
    {
        var snapshot = await bookingRepository.GetByIdAsync(bookingId, ct) ?? throw new NotFoundException("Booking not found.");
        foreach (var courtId in new[] {
                snapshot.CourtId,
                targetCourtId ?? snapshot.CourtId
            }.Distinct().Order()
        )
        {
            await unitOfWork.LockCourtAsync(courtId, ct);
        }
        await unitOfWork.LockBookingAsync(bookingId, ct);
        var booking = await bookingRepository.GetFreshDetailsAsync(bookingId, ct) ?? throw new NotFoundException("Booking not found.");
        if (booking.CourtId != snapshot.CourtId)
        {
            throw new ConflictException("Booking court changed; reload and retry.");
        }
        return booking;
    }

    public Task<bool> SettleAsync(Guid bookingId, CancellationToken ct = default)
        => unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var booking = await LockAsync(bookingId, ct: token);
            Authorize(booking);
            return await SettleLockedAsync(booking, token);
        }, ct);

    public async Task<bool> SettleLockedAsync(Booking booking, CancellationToken ct = default)
    {
        if (VietnamTime.ToLocalDateTime(booking.Date, booking.EndTime) > VietnamTime.NowLocal(clock) ||
            booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
        {
            return false;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var oldStatus = booking.Status;
        var invoice = InvoiceRules.ValidInvoice(booking);
        var paid = invoice?.Status == InvoiceStatus.Paid;
        booking.Status = paid ? BookingStatus.Completed : BookingStatus.Cancelled;
        booking.UpdatedAt = now;
        const string reason = "Automatically settled after the booking end time passed.";
        if (invoice?.Status == InvoiceStatus.Unpaid)
        {
            invoice.Status = InvoiceStatus.Cancelled;
            invoice.CancelledAt = now;
            invoice.CancellationActorType = ActorType.System;
            invoice.CancellationReason = reason;
        }

        await unitOfWork.AddAsync(new BookingStatusHistory
        {
            Id = Guid.NewGuid(),
            BookingId = booking.BookingId,
            OldStatus = oldStatus,
            NewStatus = booking.Status,
            ActorType = ActorType.System,
            ChangedAt = now,
            Reason = reason
        }, ct);
        await unitOfWork.SaveSystemChangesAsync(ct);

        return true;
    }
}
