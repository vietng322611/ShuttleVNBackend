namespace ShuttleVNBackend.Application.DTOs.User;

public record CustomerProfileDto(
    string FullName,
    string Phone,
    string Email
);