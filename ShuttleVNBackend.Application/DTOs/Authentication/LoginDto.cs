namespace ShuttleVNBackend.Application.DTOs.Authentication;

public record LoginDto(
    string UsernameOrEmail,
    string Password
);