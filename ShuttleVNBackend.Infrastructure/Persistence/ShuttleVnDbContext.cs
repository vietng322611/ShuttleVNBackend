using EFCore.ComplexIndexes.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Core.Entities.Booking;
using ShuttleVNBackend.Core.Entities.Court;
using ShuttleVNBackend.Core.Entities.System;
using ShuttleVNBackend.Core.Entities.User;

namespace ShuttleVNBackend.Infrastructure.Persistence;

public class ShuttleVnDbContext : DbContext, IUnitOfWork
{
    public ShuttleVnDbContext(DbContextOptions<ShuttleVnDbContext> options) : base(options)
    {
    }

    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<CourtSchedule> CourtSchedules => Set<CourtSchedule>();
    public DbSet<PricingRule> PricingRules => Set<PricingRule>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingStatusHistory> BookingStatusHistories => Set<BookingStatusHistory>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();

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
            entity.Property(e => e.Email).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasOne<UserAccount>()
                .WithOne()
                .HasForeignKey<Employee>(e => e.AccountId)
                .OnDelete(DeleteBehavior.NoAction)
                .IsRequired();
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId);
            entity.HasIndex(e => e.AccountId).IsUnique();
            entity.Property(e => e.Email).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasOne<UserAccount>()
                .WithOne()
                .HasForeignKey<Customer>(e => e.AccountId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        modelBuilder.Entity<Court>(entity =>
        {
            entity.HasKey(e => e.CourtId);
            entity.Property(e => e.Status).HasConversion<string>();
        });

        modelBuilder.Entity<CourtSchedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId);
            entity.HasIndex(e => new { e.CourtId, e.DayOfWeek, e.OpenTime }).IsUnique();
            entity.HasOne<Court>()
                .WithMany()
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
            entity.HasOne<Court>()
                .WithMany()
                .HasForeignKey(e => e.CourtId);
            
            entity.HasExclusionConstraint(ex => ex
                .WithEquality(e => e.CourtId)
                .WithEquality(e => e.DayOfWeek)
                .WithExpression("tsrange(DATE '2000-01-01' + \"StartTime\", DATE '2000-01-01' + \"EndTime\")", "&&")
                .HasName("ex_pricing_rules_no_overlap"));
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId);
            entity.HasIndex(e => e.BookingCode).IsUnique();
            entity.Property(e => e.TotalCost).HasPrecision(10, 2);
            entity.Property(e => e.Status).HasConversion<string>();

            entity.HasOne<Customer>()
                .WithMany()
                .HasForeignKey(e => e.CustomerId);

            entity.HasOne<Court>()
                .WithMany()
                .HasForeignKey(e => e.CourtId);
            
            // BR-08
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

            entity.HasOne<Booking>()
                .WithMany()
                .HasForeignKey(e => e.BookingId);

            entity.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(e => e.ChangedBy)
                .IsRequired(false);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId);
            entity.HasIndex(e => e.InvoiceCode).IsUnique();
            entity.Property(e => e.TotalCost).HasPrecision(10, 2);
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.PaymentMethod).HasConversion<string>();

            entity.HasOne<Booking>()
                .WithMany()
                .HasForeignKey(e => e.BookingId);

            entity.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(e => e.IssuedBy);

            // BR-13
            entity.HasIndex(e => e.BookingId)
                .IsUnique()
                .HasFilter("\"Status\" = 'Unpaid'");
        });

        modelBuilder.Entity<Audit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .IsRequired(false);
        });

        modelBuilder.Entity<VerificationCode>(entity =>
        {
            entity.HasKey(e => new { e.AccountId, e.Type });
            entity.HasOne<UserAccount>()
                .WithOne();
        });
    }

    public new async Task AddAsync<T>(T entity, CancellationToken ct = default) where T : class
        => await Set<T>().AddAsync(entity, ct);
}