using System.Text.RegularExpressions;

namespace ShuttleVNBackend.Application.UseCases.Assistant;

/// <summary>
/// Output guard: blocks invented codes, invented confirmations and instruction leaks.
/// </summary>
public static partial class AssistantReplyValidator
{
    public const string FallbackReply =
        "Mình chỉ tư vấn, không thực hiện đặt, hủy hay thanh toán. " +
        "Bạn dùng màn Đặt sân trên website hoặc liên hệ nhân viên để được hỗ trợ.";

    internal static readonly string[] ForbiddenClaims =
    [
        "đã đặt sân thành công", "đã giữ chỗ", "đã giữ sân", "đã hủy đơn",
        "đã hủy sân", "đã đổi lịch thành công", "đã thanh toán", "đã thu tiền",
        "đã hoàn tiền", "đã mở khóa tài khoản", "đơn của bạn đã được xác nhận"
    ];

    // Any of these means the model leaked or restated its own instructions.
    private static readonly string[] ForbiddenLeaks =
    [
        "system prompt", "hướng dẫn hệ thống", "chỉ dẫn hệ thống", "quy tắc hệ thống",
        "prompt hệ thống", "developer message", "bạn là trợ lý tư vấn của shuttlevn"
    ];

    public static string Sanitize(string? content, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return FallbackReply;
        }

        var text = content.Trim();
        var lower = text.ToLowerInvariant();

        if (BookingCodePattern().IsMatch(text))
        {
            return FallbackReply;
        }

        foreach (var claim in ForbiddenClaims)
        {
            if (lower.Contains(claim, StringComparison.Ordinal))
            {
                return FallbackReply;
            }
        }

        foreach (var leak in ForbiddenLeaks)
        {
            if (lower.Contains(leak, StringComparison.Ordinal))
            {
                return FallbackReply;
            }
        }

        // Scope is enforced upstream by forced tool use; these checks cover what a lookup
        // cannot prove.
        return text.Length <= maxChars ? text : Truncate(text, maxChars);
    }

    /// <summary>
    /// True when the reply cites a number no tool result and no user message supplied.
    /// </summary>
    public static bool HasUnsupportedNumbers(string reply, IReadOnlySet<string> allowed)
    {
        foreach (var token in NumericTokens(reply))
        {
            if (!allowed.Contains(token))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Numeric values the assistant may repeat. Separators are stripped so "120.000đ" equals
    /// 120000; values under three digits are ignored because they collide with ordinary prose.
    /// ponytail: numbers only; match court names when answers get verbose.
    /// </summary>
    public static HashSet<string> CollectEvidence(string payload)
    {
        return NumericTokens(payload);
    }

    private static HashSet<string> NumericTokens(string text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in NumericTokenPattern().Matches(text))
        {
            var digits = SeparatorPattern().Replace(match.Value, string.Empty);
            if (digits.Length >= MinimumComparableDigits)
            {
                tokens.Add(digits);
            }

            // A stored "18:00" and a written "lúc 18h" must compare equal.
            if (match.Value.Contains(':'))
            {
                tokens.Add(match.Value[..match.Value.IndexOf(':')]);
            }
        }

        foreach (Match match in HourPattern().Matches(text))
        {
            tokens.Add(match.Groups[1].Value);
        }

        return tokens;
    }

    private const int MinimumComparableDigits = 3;

    private static string Truncate(string text, int maxChars)
    {
        var cut = text[..maxChars];
        var lastStop = cut.LastIndexOfAny(['.', '!', '?']);

        return lastStop > 0 ? cut[..(lastStop + 1)] : cut + "…";
    }

    [GeneratedRegex(@"\bDS-[A-Za-z0-9]{5,}\b")]
    private static partial Regex BookingCodePattern();

    [GeneratedRegex(@"\d+(?:[.,:]\d+)*")]
    private static partial Regex NumericTokenPattern();

    [GeneratedRegex(@"\b(\d{1,2})\s*(?:h\b|giờ)", RegexOptions.IgnoreCase)]
    private static partial Regex HourPattern();

    [GeneratedRegex(@"[.,:]")]
    private static partial Regex SeparatorPattern();
}
