namespace DIG.Core.Models;

/// <summary>Search providers offered by the omnibox / settings.</summary>
public enum SearchEngine
{
    Google,
    DuckDuckGo,
    Brave
}

public static class SearchEngineExtensions
{
    public static string DisplayName(this SearchEngine engine) => engine switch
    {
        SearchEngine.Google => "Google",
        SearchEngine.DuckDuckGo => "DuckDuckGo",
        SearchEngine.Brave => "Brave Search",
        _ => engine.ToString()
    };

    /// <summary>Builds a search results URL for the given query.</summary>
    public static string BuildSearchUrl(this SearchEngine engine, string query)
    {
        var q = Uri.EscapeDataString(query ?? string.Empty);
        return engine switch
        {
            SearchEngine.Google => $"https://www.google.com/search?q={q}",
            SearchEngine.DuckDuckGo => $"https://duckduckgo.com/?q={q}",
            SearchEngine.Brave => $"https://search.brave.com/search?q={q}",
            _ => $"https://duckduckgo.com/?q={q}"
        };
    }
}

/// <summary>Kinds of technical identifiers D!G! can recognise in text.</summary>
public enum IdentifierKind
{
    Url,
    IpV4,
    IpV6,
    Fqdn,
    VmwareBuildNumber,
    ProductVersion,
    EsxiHostname,
    ErrorCode,
    KubernetesResource,
    LogTimestamp
}

/// <summary>A single recognised identifier plus where it was found.</summary>
public sealed record TechnicalIdentifier(IdentifierKind Kind, string Value, int Start, int Length)
{
    public string KindLabel => Kind switch
    {
        IdentifierKind.Url => "URL",
        IdentifierKind.IpV4 => "IPv4 address",
        IdentifierKind.IpV6 => "IPv6 address",
        IdentifierKind.Fqdn => "FQDN",
        IdentifierKind.VmwareBuildNumber => "VMware build number",
        IdentifierKind.ProductVersion => "Product version",
        IdentifierKind.EsxiHostname => "ESXi hostname",
        IdentifierKind.ErrorCode => "Error code",
        IdentifierKind.KubernetesResource => "Kubernetes resource",
        IdentifierKind.LogTimestamp => "Log timestamp",
        _ => Kind.ToString()
    };
}

/// <summary>What the omnibox decided an input means.</summary>
public enum OmniboxActionKind
{
    NavigateUrl,
    Search
}

public sealed record OmniboxInterpretation(
    OmniboxActionKind Action,
    string TargetUrl,
    string RawInput,
    IReadOnlyList<TechnicalIdentifier> Identifiers);

/// <summary>Confidence of a VMware build/version lookup match.</summary>
public enum MatchConfidence
{
    Verified,
    Possible,
    Unknown
}

public sealed record BuildLookupResult(
    string Query,
    MatchConfidence Confidence,
    string? Product,
    string? Version,
    string? Build,
    string? ReleaseDate,
    string? PatchAssociation,
    string? ReleaseNotesUrl,
    string Source,
    DateTimeOffset RetrievedUtc);

public sealed record Bookmark(long Id, string Title, string Url, string? Folder, string? Tags, DateTimeOffset CreatedUtc);

public sealed record HistoryEntry(long Id, string Url, string? Title, DateTimeOffset VisitedUtc);
