using System.Net;

namespace ShuttleVNBackend.Application.Exceptions;

public class TooManyRequestsException(TimeSpan retryAfter, string message = "Too many requests. Please try again later.")
    : AppException(message, (int)HttpStatusCode.TooManyRequests, "TOO_MANY_REQUESTS")
{
    public int RetryAfterSeconds { get; } = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
}

