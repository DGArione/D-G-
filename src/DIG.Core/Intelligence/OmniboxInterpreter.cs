using System.Net;
using DIG.Core.Models;

namespace DIG.Core.Intelligence;

/// <summary>
/// Decides whether omnibox text is a URL to navigate to or a query to search,
/// and surfaces any technical identifiers for context-aware actions.
/// Deterministic and unit-tested — no network access.
/// </summary>
public static class OmniboxInterpreter
{
    private static readonly string[] NavSchemes =
        { "http://", "https://", "about:", "file://", "ftp://", "chrome://", "edge://", "view-source:" };

    public static OmniboxInterpretation Interpret(string? input, SearchEngine engine)
    {
        var raw = input ?? string.Empty;
        var trimmed = raw.Trim();
        var identifiers = TechnicalIdentifierDetector.Detect(trimmed);

        if (trimmed.Length == 0)
            return Search(engine, trimmed, raw, identifiers);

        // 1. Explicit scheme always navigates.
        foreach (var scheme in NavSchemes)
        {
            if (trimmed.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
                return Navigate(trimmed, raw, identifiers);
        }

        // 2. Anything with whitespace that is not a scheme'd URL is a search.
        var hasWhitespace = trimmed.Any(char.IsWhiteSpace);
        if (hasWhitespace)
            return Search(engine, trimmed, raw, identifiers);

        // 3. Isolate the host portion (before path/query/fragment).
        var hostAndPort = trimmed;
        var cut = hostAndPort.IndexOfAny(new[] { '/', '?', '#' });
        if (cut >= 0) hostAndPort = hostAndPort[..cut];

        var host = hostAndPort;
        var portColon = host.LastIndexOf(':');
        var hasExplicitPort = portColon > 0
            && int.TryParse(host[(portColon + 1)..], out var p) && p is > 0 and <= 65535;
        if (hasExplicitPort) host = host[..portColon];

        // 4. Bare IPv4 address -> navigate (management UIs are commonly plain http).
        //    Require a dot so pure-digit strings (e.g. build numbers) are not read as IPs.
        if (host.Contains('.') && IPAddress.TryParse(host, out _))
        {
            return Navigate("http://" + trimmed, raw, identifiers);
        }

        // 5. localhost (with or without a port) -> navigate over http.
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return Navigate("http://" + trimmed, raw, identifiers);

        // 6. Looks like a domain (has a dot and an alphabetic TLD) -> navigate over https.
        if (LooksLikeDomain(host))
            return Navigate("https://" + trimmed, raw, identifiers);

        // 7. A single hostname with an explicit port (e.g. "esxi01:443") -> navigate.
        if (hasExplicitPort && IsHostLabel(host))
            return Navigate("https://" + trimmed, raw, identifiers);

        // 8. Otherwise treat as a search query.
        return Search(engine, trimmed, raw, identifiers);
    }

    private static OmniboxInterpretation Navigate(string url, string raw, IReadOnlyList<TechnicalIdentifier> ids)
        => new(OmniboxActionKind.NavigateUrl, url, raw, ids);

    private static OmniboxInterpretation Search(SearchEngine engine, string query, string raw, IReadOnlyList<TechnicalIdentifier> ids)
        => new(OmniboxActionKind.Search, engine.BuildSearchUrl(query), raw, ids);

    private static bool LooksLikeDomain(string host)
    {
        var dot = host.LastIndexOf('.');
        if (dot <= 0 || dot == host.Length - 1) return false;
        var tld = host[(dot + 1)..];
        if (tld.Length < 2 || !tld.All(char.IsLetter)) return false;
        return host.Split('.').All(IsHostLabel);
    }

    private static bool IsHostLabel(string label)
    {
        if (label.Length is 0 or > 63) return false;
        if (label[0] == '-' || label[^1] == '-') return false;
        return label.All(c => char.IsLetterOrDigit(c) || c == '-');
    }
}
