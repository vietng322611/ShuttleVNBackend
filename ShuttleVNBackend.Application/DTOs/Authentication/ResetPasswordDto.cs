namespace ShuttleVNBackend.Application.DTOs.Authentication;

public record ResetPasswordDto(
    string Email,
    string Code,
    string Password);