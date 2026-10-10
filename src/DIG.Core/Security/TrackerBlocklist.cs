namespace DIG.Core.Security;

/// <summary>
/// A small, bundled list of well-known advertising / tracking domains. Used to
/// block sub-resource requests (not top-level navigation), so pages still load
/// but their ad/tracker calls are dropped. Not a replacement for a full filter
/// list — a practical, licence-free default.
/// </summary>
public static class TrackerBlocklist
{
    private static readonly HashSet<string> Domains = new(StringComparer.OrdinalIgnoreCase)
    {
        "doubleclick.net", "googlesyndication.com", "googleadservices.com", "google-analytics.com",
        "analytics.google.com", "googletagmanager.com", "googletagservices.com", "adservice.google.com",
        "ad.doubleclick.net", "pagead2.googlesyndication.com", "stats.g.doubleclick.net",
        "facebook.net", "connect.facebook.net", "ads.facebook.com", "pixel.facebook.com",
        "scorecardresearch.com", "quantserve.com", "adnxs.com", "adsrvr.org", "rubiconproject.com",
        "pubmatic.com", "openx.net", "criteo.com", "criteo.net", "taboola.com", "outbrain.com",
        "amazon-adsystem.com", "bidswitch.net", "casalemedia.com", "contextweb.com", "33across.com",
        "moatads.com", "adform.net", "yieldmo.com", "bluekai.com", "demdex.net", "everesttech.net",
        "hotjar.com", "mixpanel.com", "segment.com", "segment.io", "fullstory.com", "mouseflow.com",
        "clarity.ms", "branch.io", "appsflyer.com", "adjust.com", "chartbeat.com", "newrelic.com",
        "sentry.io", "doubleverify.com", "serving-sys.com", "zedo.com", "advertising.com",
    };

    /// <summary>True when the host is, or is a subdomain of, a blocked domain.</summary>
    public static bool IsBlocked(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (Domains.Contains(host)) return true;
        var idx = 0;
        while ((idx = host.IndexOf('.', idx)) >= 0)
        {
            var suffix = host[(idx + 1)..];
            if (Domains.Contains(suffix)) return true;
            idx++;
        }
        return false;
    }

    /// <summary>Extracts the host from a URL (best-effort).</summary>
    public static string? HostOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        return Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : null;
    }
}
