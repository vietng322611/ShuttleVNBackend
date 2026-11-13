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
            if (context.Response.HasStarted)
            {
                logger.LogWarning(ex, "Response already started, skipping error response");
                return;
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = ex.StatusCode;

            if (ex is TooManyRequestsException tooManyRequests)
                context.Response.Headers.RetryAfter = tooManyRequests.RetryAfterSeconds.ToString();

            IDictionary<string, string[]> errors = new Dictionary<string, string[]>();
            if (ex is ValidationException validationEx)
                errors = validationEx.Errors;

            var data = ex is PriceChangedException priceChanged ? priceChanged.Quote : null;
            await context.Response.WriteAsJsonAsync(
                ApiResponseFactory.Failure<object>(ex.Message, errors, data),
                context.RequestAborted);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ExclusionViolation or PostgresErrorCodes.UniqueViolation
        })
        {
            if (context.Response.HasStarted)
            {
                logger.LogWarning(ex, "Response already started, skipping error response");
                return;
            }

            logger.LogWarning(ex, "Database constraint conflict");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status409Conflict;

            await context.Response.WriteAsJsonAsync(
                ApiResponseFactory.Failure<object>(
                    "The data was changed by another operation. Please reload and try again."),
                context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation("Request was cancelled by client");
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                logger.LogError(ex, "Unhandled exception after response already started");
                return;
            }

            logger.LogError(ex, "Unhandled exception");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            await context.Response.WriteAsJsonAsync(
                ApiResponseFactory.Failure<object>("Internal server error"),
                context.RequestAborted);
        }
    }
}
