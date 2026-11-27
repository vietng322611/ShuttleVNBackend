using System.Threading.RateLimiting;
using ShuttleVNBackend.Api.Common;

namespace ShuttleVNBackend.Api;

public static class AssistantRateLimit
{
    public const string PolicyName = "assistant";

    public static IServiceCollection AddAssistantRateLimit(this IServiceCollection services)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponseFactory.Failure<object>(
                        "Too many requests. Please try again later."),
                    ct);
            };
        });
    }
}