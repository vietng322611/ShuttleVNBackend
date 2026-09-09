namespace ShuttleVNBackend.Application.DTOs.Authentication;

public record ResetPasswordDto
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
