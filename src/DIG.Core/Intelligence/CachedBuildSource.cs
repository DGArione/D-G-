using DIG.Core.Data;

namespace DIG.Core.Intelligence;

/// <summary>Serves build records previously imported into the SQLite build cache
/// under a given source tag (e.g. CSV import, or data scraped from a Broadcom
/// page in the browser).</summary>
public sealed class CachedBuildSource : IBuildSource
{
    private readonly BuildCacheRepository _repo;
    private readonly string _sourceTag;

    public string Name { get; }
    public bool IsAuthoritative { get; }

    public CachedBuildSource(BuildCacheRepository repo, string sourceTag, string name, bool authoritative)
    {
        _repo = repo;
        _sourceTag = sourceTag;
        Name = name;
        IsAuthoritative = authoritative;
    }

    public Task<IReadOnlyList<VmwareReleaseRecord>> QueryAsync(string normalizedQuery, CancellationToken ct = default)
        => Task.FromResult(_repo.Query(normalizedQuery, _sourceTag));
}
