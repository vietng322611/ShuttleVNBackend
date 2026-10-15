using System.Text.RegularExpressions;
using ShuttleVNBackend.Application.DTOs.Users;
using ShuttleVNBackend.Application.Exceptions;

namespace ShuttleVNBackend.Application.UseCases.Users.Helpers;

public record NormalizedProfile(string FullName, string Phone, string Email);

public static partial class CustomerProfileRules
{
    [GeneratedRegex(@"[\s\-]")]
    private static partial Regex PhoneSeparators();

    [GeneratedRegex(@"^\+?\d{8,15}$")]
    private static partial Regex PhoneFormat();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailFormat();

    public static string NormalizePhone(string phone)
    {
        return PhoneSeparators().Replace(phone.Trim(), "");
    }

    public static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    public static NormalizedProfile Normalize(CustomerProfileDto dto, bool emailRequired)
    {
        var errors = new Dictionary<string, string[]>();

        var fullName = dto.FullName?.Trim() ?? string.Empty;
        if (fullName.Length == 0)
            errors["FullName"] = ["Full name is required"];

        var phone = NormalizePhone(dto.Phone ?? string.Empty);
        if (phone.Length == 0)
            errors["Phone"] = ["Phone number is required"];
        else if (!PhoneFormat().IsMatch(phone))
            errors["Phone"] = ["Phone number must have 8-15 digits and may start with +"];

        var email = NormalizeEmail(dto.Email ?? string.Empty);
        if (email.Length == 0 && emailRequired)
            errors["Email"] = ["Email is required"];
        else if (email.Length > 0 && !EmailFormat().IsMatch(email))
            errors["Email"] = ["Email is not valid"];

        if (errors.Count > 0) throw new ValidationException(errors: errors);
        return new NormalizedProfile(fullName, phone, email);
    }
}