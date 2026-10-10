using System.Text.RegularExpressions;
using DIG.Core.Models;

namespace DIG.Core.Intelligence;

/// <summary>
/// Recognises technical identifiers (IPs, FQDNs, VMware build numbers, error codes, etc.)
/// inside an arbitrary piece of text. Pure, deterministic and fully unit-testable.
/// </summary>
public static partial class TechnicalIdentifierDetector
{
    // Order matters: more specific patterns are evaluated first so that, e.g.,
    // an IP is not also reported as a bare number.
    [GeneratedRegex(@"\bhttps?://[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b")]
    private static partial Regex IpV4Regex();

    // Compact but practical IPv6 matcher (full and :: compressed forms).
    [GeneratedRegex(@"\b(?:[A-F0-9]{1,4}:){2,7}[A-F0-9]{1,4}\b|\b(?:[A-F0-9]{1,4}:){1,7}:\b|::(?:[A-F0-9]{1,4}:){0,6}[A-F0-9]{1,4}\b", RegexOptions.IgnoreCase)]
    private static partial Regex IpV6Regex();

    // VMware build numbers are 6-9 digit integers, often prefixed with "build".
    [GeneratedRegex(@"(?i)(?:build[\s:#-]*)?\b(\d{6,9})\b")]
    private static partial Regex BuildRegex();

    // Dotted product version e.g. 8.0.3, 7.0.3.0, 4.5.1-12345
    [GeneratedRegex(@"\b\d+\.\d+(?:\.\d+){1,3}(?:-\d+)?\b")]
    private static partial Regex VersionRegex();

    // FQDN / hostname: at least one dot, valid label chars, a non-numeric TLD.
    [GeneratedRegex(@"\b(?:[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,24}\b")]
    private static partial Regex FqdnRegex();

    // Common error-code shapes: VMW_, 0x0000..., ERR-1234, HTTP 503-style left to search.
    [GeneratedRegex(@"\b(?:0x[0-9A-Fa-f]{4,8}|[A-Z]{2,}[._-]?\d{2,6}|[A-Z]{2,}_[A-Z0-9_]{3,})\b")]
    private static partial Regex ErrorCodeRegex();

    // Kubernetes resource refs like pod/my-app-7d8, namespace/name, kind "Deployment".
    [GeneratedRegex(@"\b(?:pod|deployment|svc|service|namespace|ns|node|rs|replicaset|cm|configmap|sts|statefulset|ds|daemonset|ingress|pvc)/[a-z0-9][a-z0-9.-]{1,120}\b", RegexOptions.IgnoreCase)]
    private static partial Regex K8sRegex();

    // ISO-ish log timestamps: 2026-10-10T12:34:56 or 2026-10-10 12:34:56.789
    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d{1,6})?(?:Z|[+-]\d{2}:?\d{2})?\b")]
    private static partial Regex TimestampRegex();

    /// <summary>Returns every identifier found, de-duplicated by (kind,value,start).</summary>
    public static IReadOnlyList<TechnicalIdentifier> Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<TechnicalIdentifier>();

        var results = new List<TechnicalIdentifier>();
        var claimed = new List<(int start, int end)>();

        void Collect(Regex rx, IdentifierKind kind, Func<Match, (int start, int len, string val)>? selector = null)
        {
            foreach (Match m in rx.Matches(text))
            {
                var (start, len, val) = selector?.Invoke(m) ?? (m.Index, m.Length, m.Value);
                if (len <= 0) continue;
                var end = start + len;
                // Skip if this span overlaps a previously-claimed (more specific) span.
                if (claimed.Any(c => start < c.end && end > c.start))
                    continue;
                claimed.Add((start, end));
                results.Add(new TechnicalIdentifier(kind, val, start, len));
            }
        }

        Collect(UrlRegex(), IdentifierKind.Url);
        Collect(TimestampRegex(), IdentifierKind.LogTimestamp);
        Collect(IpV4Regex(), IdentifierKind.IpV4);
        Collect(IpV6Regex(), IdentifierKind.IpV6);
        Collect(K8sRegex(), IdentifierKind.KubernetesResource);
        Collect(BuildRegex(), IdentifierKind.VmwareBuildNumber, m =>
        {
            var g = m.Groups[1];
            return (g.Index, g.Length, g.Value);
        });
        Collect(VersionRegex(), IdentifierKind.ProductVersion);
        Collect(FqdnRegex(), IdentifierKind.Fqdn);
        Collect(ErrorCodeRegex(), IdentifierKind.ErrorCode);

        return results.OrderBy(r => r.Start).ToList();
    }

    /// <summary>True when the whole trimmed input is a single identifier of the given kind.</summary>
    public static bool IsExactly(string? text, IdentifierKind kind)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        var found = Detect(trimmed);
        return found.Count == 1
            && found[0].Kind == kind
            && found[0].Value.Equals(trimmed, StringComparison.OrdinalIgnoreCase);
    }
}
