using Microsoft.AspNetCore.Mvc;
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

            var problem = new ProblemDetails
            {
                Status = ex.StatusCode,
                Title = ex.Code,
                Detail = ex.Message
            };

            if (ex is ValidationException validationEx)
                problem.Extensions["errors"] = validationEx.Errors;

            await context.Response.WriteAsJsonAsync(problem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = 500,
                Title = "INTERNAL_ERROR",
                Detail = "Something went wrong"
            });
        }
    }
}