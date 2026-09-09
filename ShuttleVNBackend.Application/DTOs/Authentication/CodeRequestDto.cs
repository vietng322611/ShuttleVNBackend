using ShuttleVNBackend.Core.Entities.User.Enums;

namespace ShuttleVNBackend.Application.DTOs.Authentication;

public record CodeRequestDto
{
    public string Email { get; set; } = string.Empty;
    public CodeType Type { get; set; }
}
