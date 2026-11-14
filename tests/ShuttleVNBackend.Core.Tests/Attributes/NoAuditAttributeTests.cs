using ShuttleVNBackend.Core.Attributes;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.System;
using ShuttleVNBackend.Core.Entities.Users;

namespace ShuttleVNBackend.Core.Tests.Attributes;

// The audit interceptor skips these types, so the attribute must stay on them.
public class NoAuditAttributeTests
{
    [Theory]
    [InlineData(typeof(Audit))]
    [InlineData(typeof(IdempotencyRequest))]
    [InlineData(typeof(VerificationCode))]
    [InlineData(typeof(BookingPriceSegment))]
    [InlineData(typeof(InvoiceDailyCounter))]
    public void Type_IsMarkedNoAudit(Type type)
    {
        Assert.True(typeof(NoAuditAttribute).IsAssignableFrom(
            Attribute.GetCustomAttribute(type, typeof(NoAuditAttribute))?.GetType()));
    }

    [Fact]
    public void Property_IsMarkedNoAudit()
    {
        var property = typeof(UserAccount).GetProperty(nameof(UserAccount.PasswordHash));

        Assert.NotNull(property);
        Assert.NotNull(Attribute.GetCustomAttribute(property!, typeof(NoAuditAttribute)));
    }
}
