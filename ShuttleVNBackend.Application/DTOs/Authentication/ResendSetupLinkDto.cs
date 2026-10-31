namespace ShuttleVNBackend.Application.DTOs.Authentication;

public record ResendSetupLinkDto
{
    public string Email { get; set; } = string.Empty;
}

