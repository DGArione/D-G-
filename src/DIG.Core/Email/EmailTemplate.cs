using System.Text.RegularExpressions;

namespace DIG.Core.Email;

/// <summary>A reusable, user-editable support email template. Variables are written
/// as {{name}} in the subject/body and filled at render time.</summary>
public sealed record EmailTemplate(
    long Id,
    string Name,
    string Category,
    string Subject,
    string Body,
    bool IsBuiltIn,
    DateTimeOffset UpdatedUtc);

/// <summary>Fills {{variable}} placeholders in a template and lists the variables it uses.</summary>
public static partial class EmailTemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*([a-zA-Z0-9_]+)\s*\}\}")]
    private static partial Regex Placeholder();

    public static IReadOnlyList<string> ExtractVariables(string? text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        return Placeholder().Matches(text)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> ExtractVariables(EmailTemplate template)
        => ExtractVariables(template.Subject + "\n" + template.Body);

    /// <summary>Returns subject/body with placeholders replaced. Missing values are
    /// left visible as [name] so the author can see what still needs filling.</summary>
    public static (string Subject, string Body) Render(EmailTemplate template, IReadOnlyDictionary<string, string> values)
    {
        return (Fill(template.Subject, values), Fill(template.Body, values));
    }

    private static string Fill(string text, IReadOnlyDictionary<string, string> values)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Placeholder().Replace(text, m =>
        {
            var key = m.Groups[1].Value;
            foreach (var kv in values)
            {
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                    return kv.Value;
            }
            return $"[{key}]";
        });
    }
}
