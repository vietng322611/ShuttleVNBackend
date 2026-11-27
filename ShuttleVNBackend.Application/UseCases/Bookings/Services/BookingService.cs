using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.Interfaces.Repositories.Users;
using ShuttleVNBackend.Application.Interfaces.Users;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.Courts;
using ShuttleVNBackend.Core.Entities.System.Enums;
using ShuttleVNBackend.Core.Entities.Users;
using ShuttleVNBackend.Core.Entities.Users.Enums;
using ValidationException = ShuttleVNBackend.Application.Exceptions.ValidationException;

namespace ShuttleVNBackend.Application.UseCases.Bookings.Services;

public class BookingService(
    IBookingRepository bookingRepository,
    ICustomerRepository customerRepository,
    IVerificationCodeRepository verificationCodeRepository,
    ICurrentUser currentUser,
    TimeProvider clock,
    IUnitOfWork unitOfWork,
    IBookingIdempotency idempotency,
    BookingLifecycle lifecycle)
{
    private static readonly TimeZoneInfo VietnamTz =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<BookingDto> CreateBookingAsync(CreateBookingDto dto, string key, CancellationToken ct = default)
    {
        var scope = idempotency.GetScope();
        var response = await idempotency.ExecuteAsync(scope, key, "booking.create", null, NormalizeCreation(dto),
            async token =>
            {
                if (
                    currentUser.ActorType != ActorType.Customer ||
                    currentUser.ActorId is null ||
                    await customerRepository.GetByIdAsync(currentUser.ActorId.Value, token) is null)
                    throw new UnauthorizedException("Customer profile not found.");
            },
            async token =>
            {
                var booking = await CreateBookingCoreAsync(dto, token);
                return new BookingMutationResponse<BookingDto>(booking, BookingId: booking.BookingId);
            }, ct);
        return response.GetData();
    }

    public async Task<BookingDto> QuickBookingAsync(QuickBookingDto dto, string key, CancellationToken ct = default)
    {
        ValidateContactInfo(dto.FullName, dto.Phone, dto.Email);
        var response = await idempotency.ExecuteAsync(
            idempotency.GetScope(true, true),
            key,
            "booking.quick",
            null,
            new
            {
                Booking = NormalizeCreation(dto),
                FullName = dto.FullName.Trim(),
                Phone = NormalizePhone(dto.Phone),
                Email = dto.Email.Trim().ToLowerInvariant(),
                VerificationCodeHash = HashCode(dto.VerificationCode)
            },
            _ => Task.CompletedTask,
            token => QuickBookingCoreAsync(dto, token), ct);
        return response.GetData();
    }

    public async Task<BookingDto> CancelBookingAsync(Guid bookingId, CancelBookingDto dto, string key,
        CancellationToken ct = default)
    {
        InvoiceRules.ValidateReason(dto.Reason);
        await lifecycle.SettleAsync(bookingId, ct);
        var response = await idempotency.ExecuteAsync(idempotency.GetScope(), key, "booking.cancel", bookingId,
            new { dto.ExpectedState, Reason = dto.Reason.Trim(), dto.Refund },
            async token =>
            {
                if (currentUser.ActorId is null ||
                    currentUser.ActorType is not (ActorType.Customer or ActorType.Employee))
                    throw new UnauthorizedException();

                var booking = await lifecycle.LockAsync(bookingId, ct: token);
                lifecycle.Authorize(booking);
            },
            async token =>
            {
                var booking = await lifecycle.LockAsync(bookingId, ct: token);
                if (await lifecycle.SettleLockedAsync(booking, token))
                    return new BookingMutationResponse<BookingDto>(null,
                        "Booking end time passed; booking settled. Reload.", bookingId);
                return new BookingMutationResponse<BookingDto>(
                    await CancelBookingCoreAsync(booking, dto with { Reason = dto.Reason.Trim() }, token));
            }, ct);
        return response.GetData();
    }

    private static object NormalizeCreation(CreateBookingDto dto)
    {
        return new
        {
            dto.CourtId,
            dto.Date,
            dto.StartTime,
            dto.EndTime,
            TotalCost = dto.TotalCost?.ToString("G29", CultureInfo.InvariantCulture),
            dto.QuoteFingerprint
        };
    }

    public Task<PricePreviewResponse> GetPricePreviewAsync(PricePreviewDto dto, CancellationToken ct = default)
    {
        idempotency.GetScope(true);
        return unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            ValidateTimeSlot(dto.StartTime, dto.EndTime);
            await unitOfWork.LockCourtAsync(dto.CourtId, token);
            var schedule = await bookingRepository.GetScheduleAsync(
                dto.CourtId, GetIsoDayOfWeek(dto.Date), token);
            ValidateSchedule(schedule, dto.StartTime, dto.EndTime);
            var rules = await bookingRepository.GetPricingRulesAsync(
                dto.CourtId, GetIsoDayOfWeek(dto.Date), token);
            var (totalCost, segments) = CalculatePrice(dto.StartTime, dto.EndTime, rules);
            return BookingQuote.Create(dto.CourtId, dto.Date, dto.StartTime, dto.EndTime, totalCost, segments);
        }, ct);
    }

    private async Task<BookingDto> CreateBookingCoreAsync(CreateBookingDto dto, CancellationToken ct = default)
    {
        if (currentUser.ActorType != ActorType.Customer || currentUser.ActorId is null)
            throw new UnauthorizedException("Only authenticated customers can create bookings.");

        var customer = await customerRepository.GetByIdAsync(currentUser.ActorId.Value, ct)
                       ?? throw new UnauthorizedException("Customer profile not found.");

        return await CreateBookingAsync(
            dto,
            customer, ActorType.Customer,
            customer.CustomerId, null, ct);
    }

    private async Task<BookingMutationResponse<BookingDto>> QuickBookingCoreAsync(QuickBookingDto dto,
        CancellationToken ct = default)
    {
        ValidateContactInfo(dto.FullName, dto.Phone, dto.Email);

        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        await idempotency.LockVerificationAsync(normalizedEmail, ct);

        var code = await verificationCodeRepository.GetActiveAsync(
            normalizedEmail, CodeType.QuickBooking, ct);

        if (code is null || code.IsUsed || code.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
            throw new ValidationException("Verification code is invalid or expired.",
                new Dictionary<string, string[]> { ["VerificationCode"] = ["Invalid or expired code."] });

        var codeHash = HashCode(dto.VerificationCode);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(code.CodeHash),
                Encoding.UTF8.GetBytes(codeHash)))
        {
            code.Attempt++;
            if (code.Attempt >= 5)
                code.ExpiresAt = clock.GetUtcNow().UtcDateTime;
            await unitOfWork.SaveChangesAsync(ct);
            return new BookingMutationResponse<BookingDto>(null, "Verification code is incorrect.",
                ValidationErrors: new Dictionary<string, string[]> { ["VerificationCode"] = ["Incorrect code."] });
        }

        var customer = await customerRepository.GetByEmailAsync(normalizedEmail, ct);
        if (customer is null)
        {
            customer = new Customer
            {
                CustomerId = Guid.NewGuid(),
                FullName = dto.FullName.Trim(),
                Phone = NormalizePhone(dto.Phone),
                Email = normalizedEmail,
                CreatedAt = clock.GetUtcNow().UtcDateTime,
                UpdatedAt = clock.GetUtcNow().UtcDateTime
            };
            await unitOfWork.AddAsync(customer, ct);
        }

        var booking = await CreateBookingAsync(
            dto,
            customer, ActorType.Customer,
            customer.CustomerId, null, ct,
            code);

        return new BookingMutationResponse<BookingDto>(booking, BookingId: booking.BookingId);
    }

    private async Task<BookingDto> CancelBookingCoreAsync(
        Booking booking,
        CancelBookingDto dto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason))
            throw new ValidationException("Reason is required.",
                new Dictionary<string, string[]> { ["Reason"] = ["Reason is required."] });

        if (currentUser.ActorType == ActorType.Customer)
        {
            var customer = currentUser.ActorId is not null
                ? await customerRepository.GetByIdAsync(currentUser.ActorId.Value, ct)
                : null;
            if (customer is null || booking.CustomerId != customer.CustomerId)
                throw new NotFoundException("Booking not found.");
        }

        if (booking.Status == BookingStatus.Cancelled)
            return BookingDto.FromEntity(booking);

        InvoiceRules.CheckState(booking, dto.ExpectedState);

        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
            throw new ConflictException("Only pending or confirmed bookings can be cancelled.");

        if (currentUser.ActorType == ActorType.Customer)
        {
            var invoice = booking.Invoices.FirstOrDefault(i =>
                i.Status is InvoiceStatus.Paid or InvoiceStatus.Unpaid);
            if (invoice?.Status == InvoiceStatus.Paid)
                throw new ConflictException("Paid bookings must be cancelled through an employee.");
        }

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(
            booking.Date.ToDateTime(booking.StartTime), VietnamTz);
        if (nowUtc > startUtc.AddHours(-1))
            throw new ConflictException("Booking can only be cancelled at least 1 hour before start time.");

        var oldStatus = booking.Status;
        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = nowUtc;

        var validInvoice = InvoiceRules.ValidInvoice(booking);
        if (validInvoice is not null)
            InvoiceRules.Cancel(validInvoice, dto.Reason, currentUser.ActorType,
                currentUser.ActorId!.Value, dto.Refund, nowUtc);
        else if (dto.Refund is not null) throw new ConflictException("Booking has no valid invoice to refund.");

        var history = new BookingStatusHistory
        {
            Id = Guid.NewGuid(),
            BookingId = booking.BookingId,
            OldStatus = oldStatus,
            NewStatus = BookingStatus.Cancelled,
            ActorType = currentUser.ActorType,
            ChangedAt = nowUtc,
            Reason = dto.Reason
        };

        switch (currentUser.ActorType)
        {
            case ActorType.Customer:
            {
                var customer = await customerRepository.GetByIdAsync(currentUser.ActorId!.Value, ct);
                history.CustomerId = customer?.CustomerId;
                break;
            }
            case ActorType.Employee:
                history.ChangedByEmployee = currentUser.ActorId;
                break;
        }

        await unitOfWork.AddAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return BookingDto.FromEntity(booking);
    }

    public async Task<BookingDto?> GetBookingByCodeAsync(string code, CancellationToken ct = default)
    {
        var booking = await bookingRepository.GetByCodeAsync(code, null, ct);
        return booking is null ? null : BookingDto.FromEntity(booking);
    }

    public async Task<PagedResult<BookingDto>> GetOwnBookingsAsync(PageRequest page, CancellationToken ct = default)
    {
        if (currentUser.ActorType != ActorType.Customer || currentUser.ActorId is null)
            throw new UnauthorizedException("Only authenticated customers can view booking history.");

        var customer = await customerRepository.GetByIdAsync(currentUser.ActorId.Value, ct)
                       ?? throw new UnauthorizedException("Customer profile not found.");

        var paged = await bookingRepository.GetByCustomerWithDetailsAsync(customer.CustomerId, page, ct);

        var dtoList = paged.Items.Select(BookingDto.FromEntity).ToList();
        return new PagedResult<BookingDto>
        {
            Items = dtoList,
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount
        };
    }

    public async Task<BookingDto> GetBookingByIdAsync(Guid bookingId, CancellationToken ct = default)
    {
        var booking = await bookingRepository.GetByIdWithDetailsAsync(bookingId, ct)
                      ?? throw new NotFoundException("Booking not found.");

        if (currentUser.ActorType != ActorType.Customer)
            return BookingDto.FromEntity(booking);

        var customer = currentUser.ActorId is not null
            ? await customerRepository.GetByIdAsync(currentUser.ActorId.Value, ct)
            : null;

        if (customer is null || booking.CustomerId != customer.CustomerId)
            throw new NotFoundException("Booking not found.");

        return BookingDto.FromEntity(booking);
    }

    public async Task<InvoiceDto> GetBookingInvoiceAsync(Guid bookingId, CancellationToken ct = default)
    {
        var booking = await bookingRepository.GetByIdWithDetailsAsync(bookingId, ct)
                      ?? throw new NotFoundException("Booking not found.");

        if (currentUser.ActorType == ActorType.Customer)
        {
            var customer = currentUser.ActorId is not null
                ? await customerRepository.GetByIdAsync(currentUser.ActorId.Value, ct)
                : null;

            if (customer is null || booking.CustomerId != customer.CustomerId)
                throw new NotFoundException("Booking not found.");
        }

        var invoice = booking.Invoices
                          .OrderByDescending(i => i.IssuedAt)
                          .FirstOrDefault()
                      ?? throw new NotFoundException("Invoice not found.");

        return InvoiceDto.FromEntity(invoice);
    }

    private async Task<BookingDto> CreateBookingAsync(
        CreateBookingDto dto,
        Customer customer,
        ActorType actorType,
        Guid? actorCustomerId,
        Guid? actorEmployeeId,
        CancellationToken ct,
        VerificationCode? consumeCode = null)
    {
        ValidateTimeSlot(dto.StartTime, dto.EndTime);

        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(dto.Date.ToDateTime(dto.StartTime), VietnamTz);

        if (startUtc <= nowUtc)
            throw new ConflictException("Booking start time must be in the future.");

        await unitOfWork.LockCourtAsync(dto.CourtId, ct);
        var isoDow = GetIsoDayOfWeek(dto.Date);
        var schedule = await bookingRepository.GetScheduleAsync(dto.CourtId, isoDow, ct);
        ValidateSchedule(schedule, dto.StartTime, dto.EndTime);

        var rules = await bookingRepository.GetPricingRulesAsync(dto.CourtId, isoDow, ct);
        var (totalCost, segments) = CalculatePrice(dto.StartTime, dto.EndTime, rules);
        var quote = BookingQuote.Create(dto.CourtId, dto.Date, dto.StartTime, dto.EndTime, totalCost, segments);
        BookingQuote.ValidateConfirmation(dto, quote);

        var bookingCode = await GenerateUniqueCodeAsync(ct);
        var booking = new Booking
        {
            BookingId = Guid.NewGuid(),
            BookingCode = bookingCode,
            CustomerId = customer.CustomerId,
            CourtId = dto.CourtId,
            Date = dto.Date,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Status = BookingStatus.Pending,
            TotalCost = totalCost,
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };

        await unitOfWork.AddAsync(booking, ct);

        foreach (var seg in segments) await unitOfWork.AddAsync(seg.ToEntity(booking.BookingId), ct);

        var history = new BookingStatusHistory
        {
            Id = Guid.NewGuid(),
            BookingId = booking.BookingId,
            OldStatus = null,
            NewStatus = BookingStatus.Pending,
            ActorType = actorType,
            CustomerId = actorCustomerId,
            ChangedByEmployee = actorEmployeeId,
            ChangedAt = nowUtc,
            Reason = "Booking created"
        };
        await unitOfWork.AddAsync(history, ct);

        if (consumeCode is not null)
        {
            consumeCode.IsUsed = true;
            consumeCode.ExpiresAt = nowUtc;
        }

        await unitOfWork.SaveChangesAsync(ct);

        var saved = await bookingRepository.GetByIdWithDetailsAsync(booking.BookingId, ct)
                    ?? throw new NotFoundException("Booking not found.");
        return BookingDto.FromEntity(saved);
    }

    internal static (decimal TotalCost, IReadOnlyList<BookingPriceSegmentDto> Segments)
        CalculatePrice(TimeOnly startTime, TimeOnly endTime, List<PricingRule> rules)
    {
        if (rules.Count == 0)
            throw new ConflictException("No pricing rules configured for this court and day.");

        var sorted = rules.OrderBy(r => r.StartTime).ToList();
        var segments = new List<BookingPriceSegmentDto>();
        var segNo = 1;
        decimal totalCostRaw = 0;

        for (var i = 0; i < sorted.Count; i++)
        {
            var rule = sorted[i];
            var ruleEnd = i + 1 < sorted.Count ? sorted[i + 1].StartTime : TimeOnly.MaxValue;

            var segStart = startTime > rule.StartTime ? startTime : rule.StartTime;
            var segEnd = endTime < ruleEnd ? endTime : ruleEnd;

            if (segStart >= segEnd) continue;

            var minutes = (segEnd - segStart).TotalMinutes;
            totalCostRaw += (decimal)minutes * rule.PricePerHour / 60m;

            segments.Add(new BookingPriceSegmentDto(
                segNo++,
                rule.StartTime,
                segStart,
                segEnd,
                rule.PricePerHour));
        }

        if (segments.Count == 0 || segments[0].SegmentStartTime != startTime || segments[^1].SegmentEndTime != endTime)
            throw new ConflictException("Pricing rules do not cover the entire booking interval.");

        var totalCost = Math.Round(totalCostRaw, 0, MidpointRounding.AwayFromZero);
        return (totalCost, segments);
    }

    internal static void ValidateTimeSlot(TimeOnly startTime, TimeOnly endTime)
    {
        var errors = new Dictionary<string, string[]>();

        if (startTime.Ticks % TimeSpan.TicksPerMinute != 0 || startTime.Minute is not (0 or 30))
            errors["StartTime"] = ["Start time must be at :00 or :30 with no seconds."];
        if (endTime.Ticks % TimeSpan.TicksPerMinute != 0 || endTime.Minute is not (0 or 30))
            errors["EndTime"] = ["End time must be at :00 or :30 with no seconds."];
        if (startTime >= endTime)
            errors["EndTime"] = ["End time must be after start time."];
        if ((endTime - startTime).TotalMinutes < 30)
            errors["EndTime"] = ["Minimum booking duration is 30 minutes."];

        if (errors.Count > 0)
            throw new ValidationException("Invalid time slot.", errors);
    }

    private static void ValidateSchedule(CourtSchedule? schedule, TimeOnly startTime, TimeOnly endTime)
    {
        if (schedule is null || !schedule.IsAvailable)
            throw new ConflictException("Court is not available on this day.");
        if (startTime < schedule.OpenTime || endTime > schedule.CloseTime)
            throw new ConflictException(
                $"Booking must be within court hours ({schedule.OpenTime}–{schedule.CloseTime}).");
    }

    private static void ValidateContactInfo(string fullName, string phone, string email)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(fullName))
            errors["FullName"] = ["Full name is required."];
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            errors["Email"] = ["A valid email is required."];
        if (string.IsNullOrWhiteSpace(phone))
        {
            errors["Phone"] = ["Phone number is required."];
        }
        else
        {
            var digits = phone.Replace(" ", "").Replace("-", "");
            var digitOnly = digits.StartsWith('+') ? digits[1..] : digits;
            if (!digitOnly.All(char.IsDigit) || digitOnly.Length < 8 || digitOnly.Length > 12)
                errors["Phone"] = ["Phone must be 8–12 digits (leading + allowed)."];
        }

        if (errors.Count > 0)
            throw new ValidationException("Invalid contact information.", errors);
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken ct)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = RandomNumberGenerator.GetString(chars, 6);
            var code = $"DS-{suffix}";
            if (!await bookingRepository.CodeExistsAsync(code, ct))
                return code;
        }

        throw new InvalidOperationException("Failed to generate a unique booking code.");
    }

    private static int GetIsoDayOfWeek(DateOnly date)
    {
        return date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
    }

    private static string NormalizePhone(string phone)
    {
        return phone.Replace(" ", "").Replace("-", "");
    }

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim()));
        return Convert.ToBase64String(bytes);
    }
}