using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;

namespace ShuttleVNBackend.Infrastructure.Email;

public sealed partial class EmailTemplateRenderer
{
    private const string ResourcePrefix = "ShuttleVNBackend.Infrastructure.Email.Templates.";

    private readonly ConcurrentDictionary<string, string> _templates = new();

    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex Placeholder();

    public string Render(string fragmentName, string title, string preheader,
        IReadOnlyDictionary<string, string> values)
    {
        var encoded = values.ToDictionary(pair => pair.Key, pair => WebUtility.HtmlEncode(pair.Value));
        var content = Fill(Template(fragmentName), encoded);

        return Fill(Template("base"), new Dictionary<string, string>
        {
            ["EmailTitle"] = WebUtility.HtmlEncode(title),
            ["PreheaderText"] = WebUtility.HtmlEncode(preheader),
            ["ContentHtml"] = content
        });
    }

    private string Template(string name)
    {
        return _templates.GetOrAdd(name, static key =>
        {
            using var stream = typeof(EmailTemplateRenderer).Assembly
                                   .GetManifestResourceStream($"{ResourcePrefix}{key}.html")
                               ?? throw new InvalidOperationException($"Email template '{key}' was not found.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        });
    }

    private static string Fill(string template, IReadOnlyDictionary<string, string> values)
    {
        return Placeholder().Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value, out var value)
                ? value
                : throw new InvalidOperationException(
                    $"Missing value for email template placeholder '{match.Groups[1].Value}'."));
    }
}

