namespace ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;

public interface IInvoiceCodeGenerator
{
    Task<string> NextAsync(DateOnly issueDate, CancellationToken ct = default);
}