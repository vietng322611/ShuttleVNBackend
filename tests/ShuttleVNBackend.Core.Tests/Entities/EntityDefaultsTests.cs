using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Bookings.Enums;
using ShuttleVNBackend.Core.Entities.Courts;
using ShuttleVNBackend.Core.Entities.Courts.Enums;
using ShuttleVNBackend.Core.Entities.System;
using ShuttleVNBackend.Core.Entities.System.Enums;
using ShuttleVNBackend.Core.Entities.Users;
using ShuttleVNBackend.Core.Entities.Users.Enums;

namespace ShuttleVNBackend.Core.Tests.Entities;

// Defaults matter because EF maps them straight to NOT NULL columns.
public class EntityDefaultsTests
{
    [Fact]
    public void BadmintonCourt_DefaultsToEmptyDescription_AndDefaultStatus()
    {
        var court = new BadmintonCourt();

        Assert.Equal(string.Empty, court.Description);
        Assert.Equal(CourtStatus.Active, court.Status);
        Assert.Null(court.DeletedAt);
    }

    [Fact]
    public void UserAccount_DefaultsToEmptyPasswordHash()
    {
        var account = new UserAccount();

        Assert.Equal(string.Empty, account.PasswordHash);
        Assert.Equal(0, account.FailedLoginAttempts);
        Assert.Null(account.Customer);
        Assert.Null(account.Employee);
    }

    [Fact]
    public void Invoice_DefaultsToEmptyNote()
    {
        var invoice = new Invoice();

        Assert.Equal(string.Empty, invoice.Note);
        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
        Assert.Null(invoice.PaidAt);
    }

    [Fact]
    public void BookingStatusHistory_DefaultsToEmptyReason()
    {
        var history = new BookingStatusHistory();

        Assert.Equal(string.Empty, history.Reason);
        Assert.Equal(ActorType.System, history.ActorType);
        Assert.Null(history.OldStatus);
    }

    [Theory]
    [InlineData(BookingStatus.Pending, "Pending")]
    [InlineData(BookingStatus.Confirmed, "Confirmed")]
    [InlineData(BookingStatus.Completed, "Completed")]
    [InlineData(BookingStatus.Cancelled, "Cancelled")]
    public void BookingStatus_NamesMatchPersistedStrings(BookingStatus status, string expected)
    {
        Assert.Equal(expected, status.ToString());
    }

    [Theory]
    [InlineData(InvoiceStatus.Unpaid, "Unpaid")]
    [InlineData(InvoiceStatus.Paid, "Paid")]
    [InlineData(InvoiceStatus.Cancelled, "Cancelled")]
    [InlineData(InvoiceStatus.Refunded, "Refunded")]
    public void InvoiceStatus_NamesMatchPersistedStrings(InvoiceStatus status, string expected)
    {
        Assert.Equal(expected, status.ToString());
    }

    [Theory]
    [InlineData(CourtStatus.Active, "Active")]
    [InlineData(CourtStatus.Maintenance, "Maintenance")]
    [InlineData(CourtStatus.Closed, "Closed")]
    public void CourtStatus_NamesMatchPersistedStrings(CourtStatus status, string expected)
    {
        Assert.Equal(expected, status.ToString());
    }

    [Theory]
    [InlineData(AccountType.Customer, "Customer")]
    [InlineData(AccountType.Employee, "Employee")]
    public void AccountType_NamesMatchPersistedStrings(AccountType type, string expected)
    {
        Assert.Equal(expected, type.ToString());
    }

    [Fact]
    public void Notification_DefaultsToZeroAttempts()
    {
        var notification = new Notification();

        Assert.Equal(0, notification.AttemptCount);
        Assert.Null(notification.SentAt);
        Assert.Null(notification.LastError);
    }

    [Fact]
    public void Audit_DefaultsToSystemActor()
    {
        var audit = new Audit();

        Assert.Equal(ActorType.System, audit.ActorType);
        Assert.Null(audit.OldValue);
        Assert.Null(audit.NewValue);
    }
}
