using Microsoft.AspNetCore.Http;

namespace ShuttleVNBackend.Application.Exceptions;

public class AppException: Exception
{
    public int StatusCode { get; }
    public string Code { get; }
    
    protected AppException(string message, int statusCode, string code) 
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}

public class NotFoundException(string message = "Resource not found")
    : AppException(message, StatusCodes.Status404NotFound, "NOT_FOUND");

public class ValidationException(
    string message = "Validation failed",
    IDictionary<string, string[]>? errors = null)
    : AppException(message, StatusCodes.Status400BadRequest, "VALIDATION_ERROR")
{
    public IDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();
}

public class UnauthorizedException(string message = "Unauthorized")
    : AppException(message, StatusCodes.Status401Unauthorized, "UNAUTHORIZED");

public class ConflictException(string message = "Resource already exists")
    : AppException(message, StatusCodes.Status409Conflict, "CONFLICT");