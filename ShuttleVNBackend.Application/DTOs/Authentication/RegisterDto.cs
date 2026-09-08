namespace ShuttleVNBackend.Application.DTOs.Authentication;

public record RegisterDto(
    string FullName,
    string Phone,
    string Email,
    string Code,
    string Password,
    string ConfirmPassword
);