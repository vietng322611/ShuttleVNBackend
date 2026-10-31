using Npgsql;
using Microsoft.EntityFrameworkCore;
using ShuttleVNBackend.Api.Common;
using ShuttleVNBackend.Application.Exceptions;
using ValidationException = ShuttleVNBackend.Application.Exceptions.ValidationException;

namespace ShuttleVNBackend.Api.Middlewares;

public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = ex.StatusCode;

            if (ex is TooManyRequestsException tooManyRequests)
                context.Response.Headers["Retry-After"] = tooManyRequests.RetryAfterSeconds.ToString();

            IDictionary<string, string[]> errors = new Dictionary<string, string[]>();
            if (ex is ValidationException validationEx)
                errors = validationEx.Errors;

            var data = ex is PriceChangedException priceChanged ? priceChanged.Quote : null;
            var problem = ApiResponseFactory.Failure<object>(ex.Message, errors, data);

            await context.Response.WriteAsJsonAsync(problem);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ExclusionViolation
                                                   or PostgresErrorCodes.UniqueViolation
        })
        {
            logger.LogWarning(ex, "Database constraint conflict");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status409Conflict;

            await context.Response.WriteAsJsonAsync(ApiResponseFactory.Failure<object>(
                "The data was changed by another operation. Please reload and try again."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            await context.Response.WriteAsJsonAsync(
                ApiResponseFactory.Failure<object>("Internal server error"));
        }
    }
}
