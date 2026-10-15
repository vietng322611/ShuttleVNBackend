using EFCore.ComplexIndexes.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Core.Entities.Bookings;
using ShuttleVNBackend.Core.Entities.Courts;
using ShuttleVNBackend.Core.Entities.System;
using ShuttleVNBackend.Core.Entities.Users;

namespace ShuttleVNBackend.Infrastructure.Persistence;

public class ShuttleVnDbContext : DbContext, IUnitOfWork
{
    public ShuttleVnDbContext(DbContextOptions<ShuttleVnDbContext> options) : base(options)
    {
    }

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<BadmintonCourt> BadmintonCourts => Set<BadmintonCourt>();
    public DbSet<CourtSchedule> CourtSchedules => Set<CourtSchedule>();
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDailyCounter> InvoiceDailyCounters => Set<InvoiceDailyCounter>();
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();
    public DbSet<BookingPriceSegment> BookingPriceSegments => Set<BookingPriceSegment>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();

    public new async Task AddAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
        await Set<T>().AddAsync(entity, ct);
    }

    public new void Remove<T>(T entity) where T : class
    {
        Set<T>().Remove(entity);
    }

    public void RemoveRange<T>(IEnumerable<T> entities) where T : class
    {
        Set<T>().RemoveRange(entities);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action,
        CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is not null)
            return await action(ct);

        await using var transaction = await Database.BeginTransactionAsync(ct);
        var result = await action(ct);
        await transaction.CommitAsync(ct);
        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.HasKey(e => e.AccountId);
            entity.HasIndex(e => e.LoginEmail).IsUnique();
            entity.Property(e => e.AccountType).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.EmployeeId);
            entity.HasIndex(e => e.AccountId).IsUnique();
            entity.Property(e => e.Email).IsRequired(false);
            entity.HasIndex(e => e.Email).IsUnique().HasFilter("\"Email\" IS NOT NULL");
            entity.HasOne<UserAccount>()
                .WithOne(u => u.Employee)
                .HasForeignKey<Employee>(e => e.AccountId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId);
            entity.HasIndex(e => e.AccountId).IsUnique();
            entity.Property(e => e.Email).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Phone);
            entity.HasOne<UserAccount>()
                .WithOne(u => u.Customer)
                .HasForeignKey<Customer>(e => e.AccountId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        modelBuilder.Entity<BadmintonCourt>(entity =>
        {
            entity.HasKey(e => e.CourtId);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<CourtSchedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId);
            entity.HasIndex(e => new { e.CourtId, e.DayOfWeek, e.OpenTime }).IsUnique();
            entity.HasOne<BadmintonCourt>()
                .WithMany(c => c.CourtSchedules)
                .HasForeignKey(e => e.CourtId);

            entity.HasExclusionConstraint(ex => ex
                .WithEquality(e => e.CourtId)
                .WithEquality(e => e.DayOfWeek)
                .WithExpression("tsrange(DATE '2000-01-01' + \"OpenTime\", DATE '2000-01-01' + \"CloseTime\")", "&&")
                .HasName("ex_court_schedules_no_overlap"));
        });

        modelBuilder.Entity<PricingRule>(entity =>
        {
            entity.HasKey(e => e.PricingRuleId);
            entity.HasIndex(e => new { e.CourtId, e.DayOfWeek, e.StartTime }).IsUnique();
            entity.Property(e => e.PricePerHour).HasPrecision(10, 2);
            entity.HasOne<BadmintonCourt>()
                .WithMany(c => c.PricingRules)
                .HasForeignKey(e => e.CourtId);
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId);
            entity.HasIndex(e => e.BookingCode).IsUnique();
            entity.Property(e => e.TotalCost).HasPrecision(10, 2);
            entity.Property(e => e.Status).HasConversion<string>();

            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId);

            entity.HasOne(e => e.Court)
                .WithMany()
                .HasForeignKey(e => e.CourtId);

            entity.HasIndex(e => new { e.Status, e.CreatedAt, e.BookingId });
            entity.HasIndex(e => new { e.Date, e.CourtId, e.StartTime, e.BookingId });          

            entity.HasExclusionConstraint(ex => ex
                .WithEquality(e => e.CourtId)
                .WithEquality(e => e.Date)
                .WithExpression("tsrange(\"Date\" + \"StartTime\", \"Date\" + \"EndTime\")", "&&")
                .HasFilter("\"Status\" <> 'Cancelled'")
                .HasName("ex_bookings_no_overlap"));
        });

        modelBuilder.Entity<BookingStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OldStatus).HasConversion<string>();
            entity.Property(e => e.NewStatus).HasConversion<string>();
            entity.Property(e => e.ActorType).HasConversion<string>().HasMaxLength(20);

            entity.HasOne(e => e.Booking)
                .WithMany()
                .HasForeignKey(e => e.BookingId);

            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.ChangedByEmployee)
                .IsRequired(false);

            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .IsRequired(false);

            entity.HasIndex(e => new { e.BookingId, e.ChangedAt, e.Id });

            entity.HasIndex(e => e.BookingId)
                .IsUnique()
                .HasFilter("\"OldStatus\" IS NULL");

            entity.ToTable(t => t.HasCheckConstraint("ck_booking_status_history_actor",
                "(\"ActorType\" = 'System' AND \"ChangedByEmployee\" IS NULL AND \"CustomerId\" IS NULL) OR " +
                "(\"ActorType\" = 'Employee' AND \"ChangedByEmployee\" IS NOT NULL AND \"CustomerId\" IS NULL) OR " +
                "(\"ActorType\" = 'Customer' AND \"CustomerId\" IS NOT NULL AND \"ChangedByEmployee\" IS NULL)"));
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId);
            entity.HasIndex(e => e.InvoiceCode).IsUnique();
            entity.Property(e => e.TotalCost).HasPrecision(10, 2);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.PaymentMethod).HasConversion<string>();

            entity.HasOne(e => e.Booking)
                .WithMany()
                .HasForeignKey(e => e.BookingId);

            entity.HasOne(e => e.IssueEmployee)
                .WithMany()
                .HasForeignKey(e => e.IssuedByEmployee);

            entity.HasOne(e => e.PaidEmployee)
                .WithMany()
                .HasForeignKey(e => e.PaidByEmployee)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.BookingId)
                .IsUnique()
                .HasFilter("\"Status\" IN ('Unpaid', 'Paid')");

            entity.ToTable(t => t.HasCheckConstraint("ck_invoice_payment_group",
                "(\"Status\" = 'Unpaid' AND \"PaidAt\" IS NULL AND \"PaymentMethod\" IS NULL AND \"PaidByEmployee\" IS NULL) OR " +
                "(\"Status\" = 'Paid' AND \"PaidAt\" IS NOT NULL AND \"PaymentMethod\" IS NOT NULL AND \"PaidByEmployee\" IS NOT NULL) OR " +
                "\"Status\" = 'Cancelled'"));
        });

        modelBuilder.Entity<InvoiceDailyCounter>(entity =>
        {
            entity.HasKey(e => e.IssueDate);
            entity.Property(e => e.IssueDate).ValueGeneratedNever();
            entity.ToTable(t => t.HasCheckConstraint("ck_invoice_daily_counter_positive", "\"LastNumber\" > 0"));
        });

        modelBuilder.Entity<Audit>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ActorType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Action).HasMaxLength(20);
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.Property(e => e.EntityId).HasMaxLength(100);
            entity.Property(e => e.OldValue).HasColumnType("jsonb");
            entity.Property(e => e.NewValue).HasColumnType("jsonb");
            entity.HasOne(a => a.Employee).WithMany().HasForeignKey(a => a.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(a => a.Customer).WithMany().HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.EntityName, e.EntityId });
            entity.HasIndex(e => e.CreatedAt);

            entity.HasIndex(a => a.EmployeeId);
            entity.HasIndex(a => a.CustomerId);
            entity.ToTable(t => t.HasCheckConstraint("ck_audit_one_actor",
                "\"EmployeeId\" IS NULL OR \"CustomerId\" IS NULL"));
        });

        modelBuilder.Entity<VerificationCode>(entity => { entity.HasKey(e => new { e.Email, e.Type }); });

        modelBuilder.Entity<BookingPriceSegment>(entity =>
        {
            entity.HasKey(e => new { e.BookingId, e.SegmentNo });
            entity.Property(e => e.PricePerHour).HasPrecision(18, 0);
        });

        modelBuilder.Entity<InvoiceDailyCounter>(entity => entity.HasKey(e => e.IssueDate));

        modelBuilder.Entity<IdempotencyRequest>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ActorScope, e.RequestKey }).IsUnique();
            entity.Property(e => e.ResponseBody).HasColumnType("jsonb");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId);
            entity.HasIndex(e => e.EventKey).IsUnique();
            entity.Property(e => e.Payload).HasColumnType("jsonb");
        });
    }
}