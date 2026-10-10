using System.Reflection;
using System.Text.Json;

namespace DIG.Core.Intelligence;

/// <summary>Loads the bundled VMware build reference index from the embedded JSON.</summary>
public static class VmwareBuildIndex
{
    private const string ResourceSuffix = "vmware-builds.json";

    private sealed record Row(string product, string version, string build, string? releaseDate, string? releaseName);

    public static IReadOnlyList<VmwareReleaseRecord> LoadBundled()
    {
        try
        {
            var asm = typeof(VmwareBuildIndex).Assembly;
            var name = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase));
            if (name is null) return Array.Empty<VmwareReleaseRecord>();

            using var stream = asm.GetManifestResourceStream(name);
            if (stream is null) return Array.Empty<VmwareReleaseRecord>();

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var rows = JsonSerializer.Deserialize<List<Row>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new List<Row>();

            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.build))
                .Select(r => new VmwareReleaseRecord(r.product, r.version, r.build, r.releaseDate, null, r.releaseName))
                .ToList();
        }
        catch
        {
            return Array.Empty<VmwareReleaseRecord>();
        }
    }
}

/// <summary>
/// Offline, bundled reference index of well-known VMware builds, loaded from the
/// embedded <c>vmware-builds.json</c>. Matches are reported as "Possible" (never
/// "Verified") because this is cached reference data, not a live vendor response.
/// </summary>
public sealed class LocalIndexBuildSource : IBuildSource
{
    public string Name => "D!G! bundled reference index (curated — verify on Broadcom TechDocs)";
    public bool IsAuthoritative => false;

    private readonly List<VmwareReleaseRecord> _records;

    public LocalIndexBuildSource(IEnumerable<VmwareReleaseRecord>? extra = null)
    {
        _records = new List<VmwareReleaseRecord>(VmwareBuildIndex.LoadBundled());
        if (extra is not null) _records.AddRange(extra);
    }

    public int Count => _records.Count;
    public IReadOnlyList<VmwareReleaseRecord> Records => _records;

    public Task<IReadOnlyList<VmwareReleaseRecord>> QueryAsync(string normalizedQuery, CancellationToken ct = default)
    {
        IReadOnlyList<VmwareReleaseRecord> hits = _records
            .Where(r =>
                r.Build.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                r.Version.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                r.Version.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                r.Product.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(hits);
    }
}

/// <summary>
/// Placeholder for an authoritative online source. Live querying is intentionally
/// NOT done server-side (the Broadcom KB renders via JavaScript). Instead, D!G!
/// imports the rendered table straight from the browser tab — see the build
/// importer. This source returns nothing so lookups fall back to cached/bundled data.
/// </summary>
public sealed class UnconfiguredAuthoritativeSource : IBuildSource
{
    public string Name => "Authoritative online source (use 'Import from Broadcom page')";
    public bool IsAuthoritative => true;

    public Task<IReadOnlyList<VmwareReleaseRecord>> QueryAsync(string normalizedQuery, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<VmwareReleaseRecord>>(Array.Empty<VmwareReleaseRecord>());
}
