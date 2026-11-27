using ShuttleVNBackend.Application.Common;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;

namespace ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;

public interface IInvoiceRepository
{
    Task<PagedResult<Invoice>> SearchAsync(PageRequest page, InvoiceStatus? status, string? code,
        CancellationToken ct = default);

    Task<Invoice?> GetByIdAsync(Guid invoiceId, CancellationToken ct = default);
    Task<List<Invoice>> GetHistoryAsync(Guid bookingId, CancellationToken ct = default);
}