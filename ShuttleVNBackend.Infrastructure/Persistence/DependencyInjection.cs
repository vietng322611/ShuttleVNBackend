using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShuttleVNBackend.Infrastructure.Email;
using ShuttleVNBackend.Application.Interfaces.Repositories;
using ShuttleVNBackend.Application.Interfaces.Repositories.Bookings;
using ShuttleVNBackend.Application.Interfaces.Repositories.Courts;
using ShuttleVNBackend.Application.Interfaces.Repositories.System;
using ShuttleVNBackend.Application.Interfaces.Repositories.Users;
using ShuttleVNBackend.Application.Interfaces.Users;
using ShuttleVNBackend.Application.UseCases.Authentication.Services;
using ShuttleVNBackend.Application.UseCases.Bookings.Services;
using ShuttleVNBackend.Application.UseCases.Courts.Services;
using ShuttleVNBackend.Application.UseCases.Statistics.Services;
using ShuttleVNBackend.Application.UseCases.System;
using ShuttleVNBackend.Application.UseCases.Users.Services;
using ShuttleVNBackend.Infrastructure.Persistence.Interceptors;
using ShuttleVNBackend.Infrastructure.Persistence.Repositories.Bookings;
using ShuttleVNBackend.Infrastructure.Persistence.Repositories.Courts;
using ShuttleVNBackend.Infrastructure.Persistence.Repositories.System;
using ShuttleVNBackend.Infrastructure.Persistence.Repositories.Users;
using ShuttleVNBackend.Infrastructure.Users;

namespace ShuttleVNBackend.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ShuttleVnDbContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
            options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddSingleton(TimeProvider.System);
        services.AddEmailService(configuration);
        services.AddAccountSetup(configuration);
        services.AddHostedService<BookingNotificationWorker>();
        services.AddDataProtection();
        services.AddHttpContextAccessor();
        services.AddScoped<IBookingIdempotency, BookingIdempotency>();

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ShuttleVnDbContext>());

        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ICourtRepository, CourtRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IInvoiceCodeGenerator, InvoiceCodeGenerator>();
        services.AddScoped<IStatisticsRepository, StatisticsRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IPricingRuleRepository, PricingRuleRepository>();
        services.AddScoped<IVerificationCodeRepository, VerificationCodeRepository>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddScoped<AppAuthService>();
        services.AddScoped<PasswordSetupService>();
        services.AddScoped<AccountService>();
        services.AddScoped<CustomerService>();
        services.AddScoped<EmployeeService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<CourtService>();
        services.AddScoped<CourtScheduleService>();
        services.AddScoped<PricingRuleService>();
        services.AddScoped<AuditService>();
        services.AddScoped<StatisticsService>();
        services.AddScoped<BookingService>();
        services.AddScoped<BookingLifecycle>();
        services.AddScoped<BookingRescheduleService>();
        services.AddScoped<InvoiceService>();
        services.AddScoped<BookingRequestService>();
        services.AddScoped<AdminBookingService>();
        services.AddScoped<QuickLookupService>();

        services.AddScoped<AuditInterceptor>();

        return services;
    }
}