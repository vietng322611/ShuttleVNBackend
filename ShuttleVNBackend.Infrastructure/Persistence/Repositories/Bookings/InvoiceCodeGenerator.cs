using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;

namespace ShuttleVNBackend.Infrastructure.Persistence.Repositories.Bookings;

public class InvoiceCodeGenerator(ShuttleVnDbContext dbContext) : IInvoiceCodeGenerator
{
    public async Task<string> NextAsync(DateOnly issueDate, CancellationToken ct = default)
    {
        var numbers = await dbContext.Database.SqlQuery<long>($"""
            INSERT INTO "InvoiceDailyCounters" ("IssueDate", "LastNumber")
            VALUES ({issueDate}, 1)
            ON CONFLICT ("IssueDate")
            DO UPDATE SET "LastNumber" = "InvoiceDailyCounters"."LastNumber" + 1
            RETURNING "LastNumber" AS "Value"
            """).ToListAsync(ct);

        var date = issueDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"HD-{date}-{numbers.Single():D3}";
    }
}