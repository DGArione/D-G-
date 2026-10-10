namespace DIG.Core.Intelligence;

/// <summary>
/// Offline, bundled reference index of a small set of well-known VMware GA builds.
/// It is intentionally conservative: release dates are left null rather than
/// guessed, and matches are reported as "Possible" (never "Verified") because
/// this is cached reference data, not a live authoritative vendor response.
/// </summary>
public sealed class LocalIndexBuildSource : IBuildSource
{
    public string Name => "D!G! bundled offline reference index (verify on Broadcom TechDocs)";
    public bool IsAuthoritative => false;

    private readonly List<VmwareReleaseRecord> _records;

    public LocalIndexBuildSource(IEnumerable<VmwareReleaseRecord>? extra = null)
    {
        _records = new List<VmwareReleaseRecord>(Seed);
        if (extra is not null) _records.AddRange(extra);
    }

    public IReadOnlyList<VmwareReleaseRecord> Records => _records;

    public Task<IReadOnlyList<VmwareReleaseRecord>> QueryAsync(string normalizedQuery, CancellationToken ct = default)
    {
        IReadOnlyList<VmwareReleaseRecord> hits = _records
            .Where(r =>
                r.Build.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                r.Version.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase) ||
                r.Product.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(hits);
    }

    // A deliberately small, high-confidence set of GA build numbers.
    private static readonly VmwareReleaseRecord[] Seed =
    {
        new("VMware ESXi", "8.0", "20513097"),
        new("VMware ESXi", "7.0", "15843807"),
        new("VMware ESXi", "6.7", "8169922"),
        new("VMware ESXi", "6.5", "4564106"),
        new("VMware vCenter Server", "8.0", "20519528"),
        new("VMware vCenter Server", "7.0", "15952498"),
    };
}

/// <summary>
/// Placeholder for an authoritative online source (e.g. Broadcom TechDocs / KB).
/// Live querying is intentionally NOT implemented here: it must honour the
/// vendor's access restrictions, licensing, robots policy and rate limits, and
/// be configured with an approved endpoint. Until configured it returns nothing,
/// so lookups fall back to the bundled index and never claim to be "Verified".
/// </summary>
public sealed class UnconfiguredAuthoritativeSource : IBuildSource
{
    public string Name => "Authoritative online source (not configured)";
    public bool IsAuthoritative => true;

    public Task<IReadOnlyList<VmwareReleaseRecord>> QueryAsync(string normalizedQuery, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<VmwareReleaseRecord>>(Array.Empty<VmwareReleaseRecord>());
}
