using System.Text.RegularExpressions;

namespace DIG.Core.Security;

public sealed record RedactionHit(string Category, int Count);

public sealed record RedactionResult(string Text, IReadOnlyList<RedactionHit> Hits)
{
    public bool AnyRedacted => Hits.Count > 0;
}

public sealed record RedactionOptions(bool RedactNetworkIdentifiers = false)
{
    public static readonly RedactionOptions Default = new();
}

/// <summary>
/// Removes credentials and other sensitive tokens from text before it is logged,
/// exported, or sent to an external AI model. Optionally also masks network
/// identifiers (IPs / FQDNs) which may be customer-sensitive.
/// </summary>
public static partial class SensitiveDataRedactor
{
    private const string Mask = "[REDACTED]";

    [GeneratedRegex(@"-----BEGIN (?:RSA |EC |OPENSSH |DSA |PGP )?PRIVATE KEY-----[\s\S]*?-----END (?:RSA |EC |OPENSSH |DSA |PGP )?PRIVATE KEY-----")]
    private static partial Regex PrivateKey();

    [GeneratedRegex(@"\bAIza[0-9A-Za-z\-_]{35}\b")]
    private static partial Regex GoogleApiKey();

    [GeneratedRegex(@"\bAKIA[0-9A-Z]{16}\b")]
    private static partial Regex AwsAccessKey();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\b")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"(?i)\bbearer\s+[A-Za-z0-9._\-]+")]
    private static partial Regex Bearer();

    // key/value assignments for secret-ish names: password, pwd, secret, token, api_key, apikey
    [GeneratedRegex(@"(?i)\b(pass(?:word)?|pwd|secret|token|api[_-]?key)\b\s*[:=]\s*(""[^""]*""|'[^']*'|[^\s,;""']+)")]
    private static partial Regex KeyValueSecret();

    // credentials embedded in a URL: scheme://user:pass@host
    [GeneratedRegex(@"(?i)\b([a-z][a-z0-9+.\-]*://)([^:/\s]+):([^@/\s]+)@")]
    private static partial Regex UrlCredentials();

    [GeneratedRegex(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b")]
    private static partial Regex IpV4();

    [GeneratedRegex(@"\b(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,24}\b")]
    private static partial Regex Fqdn();

    public static RedactionResult Redact(string? text, RedactionOptions? options = null)
    {
        options ??= RedactionOptions.Default;
        if (string.IsNullOrEmpty(text))
            return new RedactionResult(text ?? string.Empty, Array.Empty<RedactionHit>());

        var hits = new List<RedactionHit>();
        var s = text;

        s = Apply(s, PrivateKey(), Mask, "private key", hits);
        s = Apply(s, GoogleApiKey(), Mask, "API key", hits);
        s = Apply(s, AwsAccessKey(), Mask, "AWS access key", hits);
        s = Apply(s, Jwt(), Mask, "JWT", hits);
        s = Apply(s, Bearer(), "Bearer " + Mask, "bearer token", hits);
        s = ApplyEval(s, KeyValueSecret(), m => $"{m.Groups[1].Value}={Mask}", "credential", hits);
        s = ApplyEval(s, UrlCredentials(), m => $"{m.Groups[1].Value}{m.Groups[2].Value}:{Mask}@", "URL credential", hits);

        if (options.RedactNetworkIdentifiers)
        {
            s = Apply(s, Fqdn(), "[HOST]", "FQDN", hits);
            s = Apply(s, IpV4(), "[IP]", "IPv4 address", hits);
        }

        return new RedactionResult(s, hits);
    }

    private static string Apply(string input, Regex rx, string replacement, string category, List<RedactionHit> hits)
    {
        var count = rx.Matches(input).Count;
        if (count == 0) return input;
        hits.Add(new RedactionHit(category, count));
        return rx.Replace(input, replacement);
    }

    private static string ApplyEval(string input, Regex rx, MatchEvaluator eval, string category, List<RedactionHit> hits)
    {
        var count = rx.Matches(input).Count;
        if (count == 0) return input;
        hits.Add(new RedactionHit(category, count));
        return rx.Replace(input, eval);
    }
}
