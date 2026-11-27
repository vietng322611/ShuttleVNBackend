using System.Net;

namespace ShuttleVNBackend.Application.Exceptions;

public class AssistantUnavailableException(
    string message = "Trợ lý tạm thời không phản hồi. Vui lòng thử lại sau.")
    : AppException(message, (int)HttpStatusCode.ServiceUnavailable, "ASSISTANT_UNAVAILABLE");