using ShuttleVNBackend.Core.Entities.User.Enums;

namespace ShuttleVNBackend.Application.DTOs.Authentication;

public record CodeRequestDto(
    string Email,
    CodeType Type
);
