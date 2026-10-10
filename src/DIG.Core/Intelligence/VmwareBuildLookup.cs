using DIG.Core.Models;

namespace DIG.Core.Intelligence;

/// <summary>A single VMware release record from some source (bundled index or live adapter).</summary>
public sealed record VmwareReleaseRecord(
    string Product,
    string Version,
    string Build,
    string? ReleaseDate = null,
    string? PatchAssociation = null,
    string? ReleaseNotesUrl = null);

/// <summary>
/// A pluggable source of VMware release data. Implementations may read a bundled
/// offline index or query an authoritative online source. Keeping this as an
/// adapter interface avoids hard-coding all release data into the application.
/// </summary>
public interface IBuildSource
{
    string Name { get; }

    /// <summary>True only for sources backed by authoritative vendor data.</summary>
    bool IsAuthoritative { get; }

    Task<IReadOnlyList<VmwareReleaseRecord>> QueryAsync(string normalizedQuery, CancellationToken ct = default);
}

/// <summary>Normalises build/version query text before lookup.</summary>
public static class BuildQueryNormalizer
{
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input.Trim();
        // Drop a leading "build" word/label and surrounding punctuation.
        var lower = s.ToLowerInvariant();
        foreach (var prefix in new[] { "build number", "build#", "build:", "build ", "build-", "build" })
        {
            if (lower.StartsWith(prefix, StringComparison.Ordinal))
            {
                s = s[prefix.Length..].Trim();
                break;
            }
        }
        return s.Replace(",", string.Empty).Replace(" ", " ").Trim();
    }

    public static bool LooksLikeBuildNumber(string normalized)
        => normalized.Length is >= 6 and <= 9 && normalized.All(char.IsDigit);
}

/// <summary>
/// Orchestrates build lookups across one or more sources and reports an honest
/// confidence level (Verified / Possible / Unknown). Never fabricates release
/// metadata: unknown fields stay null and bundled-only hits are "Possible".
/// </summary>
public sealed class VmwareBuildLookupService
{
    private readonly IReadOnlyList<IBuildSource> _sources;

    public VmwareBuildLookupService(IEnumerable<IBuildSource> sources)
        => _sources = sources.ToList();

    public async Task<BuildLookupResult> LookupAsync(string? query, CancellationToken ct = default)
    {
        var normalized = BuildQueryNormalizer.Normalize(query);
        var now = DateTimeOffset.UtcNow;

        if (normalized.Length == 0)
            return Unknown(query ?? string.Empty, "No input", now);

        VmwareReleaseRecord? bestAuthoritative = null;
        VmwareReleaseRecord? bestBundled = null;
        string sourceName = "none";

        foreach (var source in _sources)
        {
            IReadOnlyList<VmwareReleaseRecord> hits;
            try
            {
                hits = await source.QueryAsync(normalized, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // A failing source must not break the lookup; other sources still apply.
                continue;
            }

            var match = hits.FirstOrDefault(h =>
                h.Build.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
                h.Version.Equals(normalized, StringComparison.OrdinalIgnoreCase));

            if (match is null) continue;

            if (source.IsAuthoritative)
            {
                bestAuthoritative ??= match;
                sourceName = source.Name;
                break; // authoritative wins immediately
            }

            if (bestBundled is null)
            {
                bestBundled = match;
                sourceName = source.Name;
            }
        }

        var chosen = bestAuthoritative ?? bestBundled;
        if (chosen is null)
            return Unknown(normalized, "No verified release match found", now);

        var confidence = bestAuthoritative is not null ? MatchConfidence.Verified : MatchConfidence.Possible;

        return new BuildLookupResult(
            Query: normalized,
            Confidence: confidence,
            Product: chosen.Product,
            Version: chosen.Version,
            Build: chosen.Build,
            ReleaseDate: chosen.ReleaseDate,
            PatchAssociation: chosen.PatchAssociation,
            ReleaseNotesUrl: chosen.ReleaseNotesUrl,
            Source: sourceName,
            RetrievedUtc: now);
    }

    private static BuildLookupResult Unknown(string query, string source, DateTimeOffset now)
        => new(query, MatchConfidence.Unknown, null, null, null, null, null, null, source, now);
}
