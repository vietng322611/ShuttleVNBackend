using System.Text.Json;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.Interfaces.Repositories.Courts;
using ShuttleVNBackend.Application.Interfaces.Users;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.Courts.Enums;
using ShuttleVNBackend.Core.Entities.System;
using ShuttleVNBackend.Core.Entities.System.Enums;

namespace ShuttleVNBackend.Application.UseCases.Bookings.Services;

public sealed class BookingRescheduleService(
    IBookingRepository bookingRepository,
    ICourtRepository courtRepository,
    IInvoiceCodeGenerator invoiceCodeGenerator,
    BookingLifecycle lifecycle,
    IUnitOfWork unitOfWork,
    IBookingIdempotency idempotency,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<ReschedulePreviewDto> PreviewAsync(Guid bookingId, PricePreviewDto dto,
        CancellationToken ct = default)
    {
        await lifecycle.SettleAsync(bookingId, ct);
        return await unitOfWork.ExecuteInTransactionAsync(token => PreviewCoreAsync(bookingId, dto, token), ct);
    }

    private async Task<ReschedulePreviewDto> PreviewCoreAsync(Guid bookingId, PricePreviewDto dto, CancellationToken ct)
    {
        var booking = await lifecycle.LockAsync(bookingId, dto.CourtId, ct);
        lifecycle.Authorize(booking);
        ValidateBooking(booking, dto);

        var quote = await QuoteAsync(booking, dto, ct);
        var invoice = InvoiceRules.ValidInvoice(booking);
        return new ReschedulePreviewDto(InvoiceRules.State(booking), quote,
            invoice?.Status == InvoiceStatus.Paid && quote.TotalCost != booking.TotalCost, invoice?.InvoiceId);
    }

    public async Task<BookingDto> RescheduleAsync(Guid bookingId, RescheduleBookingDto dto, string key,
        CancellationToken ct = default)
    {
        InvoiceRules.ValidateReason(dto.Reason);
        await lifecycle.SettleAsync(bookingId, ct);

        var response = await idempotency.ExecuteAsync(
            idempotency.GetScope(), key, "booking.reschedule", bookingId,
            dto with { Reason = dto.Reason.Trim() },
            token => AuthorizeRescheduleAsync(bookingId, dto.CourtId, token),
            token => RescheduleCoreAsync(bookingId, dto, token), ct);

        return response.GetData();
    }

    private async Task AuthorizeRescheduleAsync(Guid bookingId, int courtId, CancellationToken ct)
    {
        var booking = await lifecycle.LockAsync(bookingId, courtId, ct);
        lifecycle.Authorize(booking);
    }

    private async Task<BookingMutationResponse<BookingDto>> RescheduleCoreAsync(Guid bookingId,
        RescheduleBookingDto dto, CancellationToken ct)
    {
        var booking = await lifecycle.LockAsync(bookingId, dto.CourtId, ct);
        if (await lifecycle.SettleLockedAsync(booking, ct))
            return new BookingMutationResponse<BookingDto>(null,
                "Booking end time passed; booking settled. Reload.", bookingId);

        InvoiceRules.CheckState(booking, dto.ExpectedState);
        var priceRequest = dto.ToPricePreviewDto();
        ValidateBooking(booking, priceRequest);
        var quote = await QuoteAsync(booking, priceRequest, ct);
        BookingQuote.ValidateConfirmation(dto.ToCreateBookingDto(), quote);

        if (IsUnchanged(booking, priceRequest))
        {
            if (dto.Refund is not null) throw new ValidationException("An unchanged booking cannot receive a refund.");

            return new BookingMutationResponse<BookingDto>(BookingDto.FromEntity(booking), BookingId: bookingId);
        }

        await ReplaceInvoiceAsync(booking, quote.TotalCost, dto, ct);
        ReplacePriceSnapshot(booking, quote);
        ApplyReschedule(booking, dto, quote.TotalCost);
        await EnqueueAsync(booking, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var saved = await bookingRepository.GetFreshDetailsAsync(bookingId, ct)
                    ?? throw new NotFoundException("Booking not found.");
        return new BookingMutationResponse<BookingDto>(BookingDto.FromEntity(saved), BookingId: bookingId);
    }

    private void ReplacePriceSnapshot(Booking booking, PricePreviewResponse quote)
    {
        var existing = booking.PriceSegments.ToDictionary(segment => segment.SegmentNo);
        var segmentNumbers = quote.Segments.Select(segment => segment.SegmentNo).ToHashSet();
        var removed = booking.PriceSegments.Where(segment => !segmentNumbers.Contains(segment.SegmentNo)).ToList();
        unitOfWork.RemoveRange(removed);
        foreach (var segment in removed) booking.PriceSegments.Remove(segment);

        foreach (var segment in quote.Segments)
            if (existing.TryGetValue(segment.SegmentNo, out var saved))
            {
                saved.PriceStartTime = segment.PriceStartTime;
                saved.SegmentStartTime = segment.SegmentStartTime;
                saved.SegmentEndTime = segment.SegmentEndTime;
                saved.PricePerHour = segment.PricePerHour;
            }
            else
            {
                booking.PriceSegments.Add(segment.ToEntity(booking.BookingId));
            }
    }

    private void ApplyReschedule(Booking booking, RescheduleBookingDto dto, decimal totalCost)
    {
        booking.CourtId = dto.CourtId;
        booking.Date = dto.Date;
        booking.StartTime = dto.StartTime;
        booking.EndTime = dto.EndTime;
        booking.TotalCost = totalCost;
        booking.UpdatedAt = clock.GetUtcNow().UtcDateTime;
    }

    private void ValidateBooking(Booking booking, PricePreviewDto dto)
    {
        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
            throw new ConflictException("Only pending or confirmed bookings can be rescheduled.");

        var cutoff = VietnamTime.NowLocal(clock).AddHours(1);
        if (VietnamTime.ToLocalDateTime(booking.Date, booking.StartTime) < cutoff ||
            VietnamTime.ToLocalDateTime(dto.Date, dto.StartTime) < cutoff)
            throw new ConflictException("Original and new start times must be at least one hour after server time.");

        if (currentUser.ActorType == ActorType.Customer &&
            InvoiceRules.ValidInvoice(booking)?.Status == InvoiceStatus.Paid)
            throw new ConflictException("Paid bookings must be rescheduled through an employee.");

        BookingService.ValidateTimeSlot(dto.StartTime, dto.EndTime);
    }

    private async Task<PricePreviewResponse> QuoteAsync(Booking booking, PricePreviewDto dto, CancellationToken ct)
    {
        if (IsUnchanged(booking, dto))
        {
            var segments = booking.PriceSegments
                .OrderBy(segment => segment.SegmentNo)
                .Select(BookingPriceSegmentDto.FromEntity)
                .ToList();
            return BookingQuote.Create(dto.CourtId, dto.Date, dto.StartTime, dto.EndTime, booking.TotalCost, segments);
        }

        var court = await courtRepository.GetByIdAsync(dto.CourtId, ct);
        if (court?.Status != CourtStatus.Active) throw new ConflictException("Target court is not active.");

        var day = dto.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)dto.Date.DayOfWeek;
        var schedules = await courtRepository.GetSchedulesAsync(dto.CourtId, ct) ?? [];
        var isOpen = schedules.Any(schedule => schedule.DayOfWeek == day && schedule.IsAvailable &&
                                               schedule.OpenTime <= dto.StartTime && schedule.CloseTime >= dto.EndTime);
        if (!isOpen) throw new ConflictException("Target slot is outside available opening hours.");

        if (await bookingRepository.HasOverlapAsync(dto.CourtId, dto.Date, dto.StartTime, dto.EndTime,
                booking.BookingId, ct))
            throw new ConflictException("Target slot overlaps another booking.");

        var rules = await bookingRepository.GetPricingRulesAsync(dto.CourtId, day, ct);
        var (cost, priceSegments) = BookingService.CalculatePrice(dto.StartTime, dto.EndTime, rules);
        return BookingQuote.Create(dto.CourtId, dto.Date, dto.StartTime, dto.EndTime, cost, priceSegments);
    }

    private async Task ReplaceInvoiceAsync(Booking booking, decimal newTotal, RescheduleBookingDto dto,
        CancellationToken ct)
    {
        var invoice = InvoiceRules.ValidInvoice(booking);
        if (booking.Status == BookingStatus.Pending)
        {
            if (invoice is not null || dto.Refund is not null)
                throw new ConflictException("Pending bookings must not have a valid invoice or refund.");

            return;
        }

        if (invoice is null || invoice.TotalCost != booking.TotalCost)
            throw new ConflictException("Booking has no matching valid invoice.");

        if (invoice.Status == InvoiceStatus.Paid && newTotal == invoice.TotalCost)
        {
            if (dto.Refund is not null)
                throw new ValidationException(
                    "Same-total paid reschedules retain their paid invoice; no refund is allowed.");

            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        InvoiceRules.Cancel(invoice, dto.Reason, currentUser.ActorType, currentUser.ActorId!.Value, dto.Refund, now);
        await unitOfWork.SaveChangesAsync(ct);

        var replacement = new Invoice
        {
            InvoiceId = Guid.NewGuid(),
            InvoiceCode = await invoiceCodeGenerator.NextAsync(VietnamTime.TodayLocal(clock), ct),
            BookingId = booking.BookingId,
            TotalCost = newTotal,
            Status = InvoiceStatus.Unpaid,
            IssuedByEmployee = currentUser.ActorType == ActorType.Employee ? currentUser.ActorId : null,
            IssuedAt = now,
            ReplacesInvoiceId = invoice.InvoiceId,
            Note = dto.Reason.Trim()
        };
        await unitOfWork.AddAsync(replacement, ct);
    }

    private Task EnqueueAsync(Booking booking, CancellationToken ct)
    {
        return unitOfWork.AddAsync(new Notification
        {
            NotificationId = Guid.NewGuid(),
            EventKey = $"reschedule:{Guid.NewGuid():N}",
            CustomerId = booking.CustomerId,
            RecipientEmail = booking.Customer.Email,
            BookingId = booking.BookingId,
            EventType = "BookingRescheduled",
            Status = "PENDING",
            CreatedAt = clock.GetUtcNow().UtcDateTime,
            Payload = JsonSerializer.Serialize(new
            {
                booking.BookingCode,
                booking.CourtId,
                booking.Date,
                booking.StartTime,
                booking.EndTime,
                booking.TotalCost
            })
        }, ct);
    }

    private static bool IsUnchanged(Booking booking, PricePreviewDto dto)
    {
        return booking.CourtId == dto.CourtId &&
               booking.Date == dto.Date &&
               booking.StartTime == dto.StartTime &&
               booking.EndTime == dto.EndTime;
    }
}