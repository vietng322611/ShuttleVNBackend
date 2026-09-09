namespace ShuttleVNBackend.Application.DTOs.User;

public record CustomerProfileDto
{
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Emai { get; set; } = string.Empty;
}