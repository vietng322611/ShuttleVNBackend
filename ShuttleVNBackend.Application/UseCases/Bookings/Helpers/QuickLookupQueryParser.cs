using ShuttleVNBackend.Application.DTOs.Bookings;
using ShuttleVNBackend.Application.Exceptions;
using ShuttleVNBackend.Application.UseCases.Users.Helpers;

namespace ShuttleVNBackend.Application.UseCases.Bookings.Helpers;

public static class QuickLookupQueryParser
{
    private const string BookingCodePrefix = "DS-";

    public static QuickLookupQuery Parse(string? keyword)
    {
        var term = keyword?.Trim();
        if (string.IsNullOrEmpty(term))
            throw Invalid("Keyword is required.");

        if (term.Contains('@'))
            return new QuickLookupQuery(QuickLookupMatchType.Email,
                CustomerProfileRules.NormalizeEmail(term));

        if (term.StartsWith(BookingCodePrefix, StringComparison.OrdinalIgnoreCase))
            return new QuickLookupQuery(QuickLookupMatchType.BookingCode, term.ToUpperInvariant());

        var phone = CustomerProfileRules.NormalizePhone(term);
        if (!CustomerProfileRules.IsValidPhone(phone))
            throw Invalid("Enter a phone number (8-15 digits), an email or a booking code (DS-...).");

        return new QuickLookupQuery(QuickLookupMatchType.Phone, phone);
    }

    private static ValidationException Invalid(string message)
    {
        return new ValidationException(errors: new Dictionary<string, string[]> { ["Keyword"] = [message] });
    }
}