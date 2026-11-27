using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.Interfaces.Users;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.System.Enums;

namespace ShuttleVNBackend.Application.UseCases.Bookings.Services;

public sealed class InvoiceService(
    IInvoiceRepository invoiceRepository,
    BookingLifecycle lifecycle,
    IBookingIdempotency idempotency,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<PagedResult<InvoiceDetailDto>> SearchAsync(PageRequest page, InvoiceStatus? status,
        string? code, CancellationToken ct = default)
    {
        RequireEmployee();
        if (status is not null && !Enum.IsDefined(status.Value))
            throw new ValidationException("Invalid invoice status.");

        var result = await invoiceRepository.SearchAsync(page, status, code, ct);
        return new PagedResult<InvoiceDetailDto>
        {
            Items = result.Items
                .Select(invoice => InvoiceDetailDto.FromEntity(invoice, InvoiceRules.State(invoice.Booking)))
                .ToList(),
            PageNumber = result.PageNumber,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount
        };
    }

    public async Task<InvoiceDetailDto> GetAsync(Guid invoiceId, CancellationToken ct = default)
    {
        RequireEmployee();
        var invoice = await invoiceRepository.GetByIdAsync(invoiceId, ct)
                      ?? throw new NotFoundException("Invoice not found.");

        return InvoiceDetailDto.FromEntity(invoice, InvoiceRules.State(invoice.Booking));
    }

    public async Task<IReadOnlyList<InvoiceDetailDto>> HistoryAsync(Guid invoiceId, CancellationToken ct = default)
    {
        RequireEmployee();
        var invoice = await invoiceRepository.GetByIdAsync(invoiceId, ct)
                      ?? throw new NotFoundException("Invoice not found.");
        var history = await invoiceRepository.GetHistoryAsync(invoice.BookingId, ct);

        return history
            .Select(item => InvoiceDetailDto.FromEntity(item, InvoiceRules.State(item.Booking)))
            .ToList();
    }

    public async Task<InvoiceDetailDto> PayAsync(Guid invoiceId, PayInvoiceDto dto, string key,
        CancellationToken ct = default)
    {
        RequireEmployee();
        var invoice = await invoiceRepository.GetByIdAsync(invoiceId, ct)
                      ?? throw new NotFoundException("Invoice not found.");
        await lifecycle.SettleAsync(invoice.BookingId, ct);

        var response = await idempotency.ExecuteAsync(
            idempotency.GetScope(), key, "invoice.pay", invoice.BookingId,
            new { InvoiceId = invoiceId, dto.ExpectedState, dto.Amount, dto.PaymentMethod, dto.PaidAt },
            token => AuthorizePaymentAsync(invoice.BookingId, token),
            token => PayCoreAsync(invoice.BookingId, invoiceId, dto, token), ct);

        return response.GetData();
    }

    private async Task AuthorizePaymentAsync(Guid bookingId, CancellationToken ct)
    {
        RequireEmployee();
        var booking = await lifecycle.LockAsync(bookingId, ct: ct);
        lifecycle.Authorize(booking);
    }

    private async Task<BookingMutationResponse<InvoiceDetailDto>> PayCoreAsync(Guid bookingId, Guid invoiceId,
        PayInvoiceDto dto, CancellationToken ct)
    {
        var booking = await lifecycle.LockAsync(bookingId, ct: ct);
        if (await lifecycle.SettleLockedAsync(booking, ct))
            return new BookingMutationResponse<InvoiceDetailDto>(null,
                "Booking end time passed; reload before recording payment. Do not repeat an external money transfer.",
                booking.BookingId);

        var invoice = booking.Invoices.Single(item => item.InvoiceId == invoiceId);
        if (booking.Status == BookingStatus.Cancelled ||
            invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Refunded)
            throw new ConflictException("Cancelled bookings or invalid invoices cannot receive payment.");

        if (invoice.Status == InvoiceStatus.Paid)
        {
            if (dto.Amount != invoice.PaidAmount || dto.PaidAt != invoice.PaidAt ||
                dto.PaymentMethod != invoice.PaymentMethod)
                throw new ConflictException(
                    "Invoice already paid with different payment details. Do not transfer money again.");

            return new BookingMutationResponse<InvoiceDetailDto>(
                InvoiceDetailDto.FromEntity(invoice, InvoiceRules.State(booking)), BookingId: booking.BookingId);
        }

        InvoiceRules.CheckState(booking, dto.ExpectedState);
        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.Completed) ||
            invoice.TotalCost != booking.TotalCost)
            throw new ConflictException("Invoice does not match a confirmed or completed booking.");

        InvoiceRules.ValidatePayment(dto.Amount, invoice.TotalCost, dto.PaymentMethod, dto.PaidAt,
            clock.GetUtcNow().UtcDateTime);
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaymentMethod = dto.PaymentMethod;
        invoice.PaidAt = dto.PaidAt;
        invoice.PaidAmount = dto.Amount;
        invoice.PaidByEmployee = currentUser.ActorId;
        await unitOfWork.SaveChangesAsync(ct);

        return new BookingMutationResponse<InvoiceDetailDto>(
            InvoiceDetailDto.FromEntity(invoice, InvoiceRules.State(booking)), BookingId: booking.BookingId);
    }

    private void RequireEmployee()
    {
        if (currentUser.ActorType != ActorType.Employee || currentUser.ActorId is null)
            throw new ForbiddenException("Employee access is required.");
    }
}