using DIG.Core.Intelligence;
using Microsoft.Data.Sqlite;

namespace DIG.Core.Data;

/// <summary>
/// Persists VMware build records imported by the user (CSV or from a Broadcom
/// page rendered in the browser) into the build_lookup_cache table, tagged by
/// source so different imports can be served or cleared independently.
/// </summary>
public sealed class BuildCacheRepository
{
    private readonly SqliteConnectionFactory _factory;
    public BuildCacheRepository(SqliteConnectionFactory factory) => _factory = factory;

    public int AddRange(IEnumerable<VmwareReleaseRecord> records, string sourceTag, string confidence, string? sourceUrl = null)
    {
        using var c = _factory.Open();
        using var tx = c.BeginTransaction();
        var now = DateTimeOffset.UtcNow.ToString("o");
        var n = 0;
        foreach (var r in records)
        {
            if (string.IsNullOrWhiteSpace(r.Build)) continue;
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO build_lookup_cache(query, product, version, build, release_date, source, source_url, confidence, retrieved_utc, pinned) " +
                "VALUES($q, $p, $v, $b, $d, $s, $u, $conf, $t, 0);";
            cmd.Parameters.AddWithValue("$q", r.Build);
            cmd.Parameters.AddWithValue("$p", r.Product);
            cmd.Parameters.AddWithValue("$v", r.Version);
            cmd.Parameters.AddWithValue("$b", r.Build);
            cmd.Parameters.AddWithValue("$d", (object?)r.ReleaseDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$s", sourceTag);
            cmd.Parameters.AddWithValue("$u", (object?)sourceUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$conf", confidence);
            cmd.Parameters.AddWithValue("$t", now);
            cmd.ExecuteNonQuery();
            n++;
        }
        tx.Commit();
        return n;
    }

    public IReadOnlyList<VmwareReleaseRecord> Query(string normalized, string? sourceTag = null)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "SELECT product, version, build, release_date FROM build_lookup_cache " +
            "WHERE (build = $q OR version LIKE $like) " +
            (sourceTag is null ? string.Empty : "AND source = $s ") +
            "LIMIT 25;";
        cmd.Parameters.AddWithValue("$q", normalized);
        cmd.Parameters.AddWithValue("$like", "%" + normalized + "%");
        if (sourceTag is not null) cmd.Parameters.AddWithValue("$s", sourceTag);

        using var r = cmd.ExecuteReader();
        var list = new List<VmwareReleaseRecord>();
        while (r.Read())
        {
            list.Add(new VmwareReleaseRecord(
                r.GetString(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3)));
        }
        return list;
    }

    public IReadOnlyList<VmwareReleaseRecord> All()
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT product, version, build, release_date FROM build_lookup_cache ORDER BY product, version;";
        using var r = cmd.ExecuteReader();
        var list = new List<VmwareReleaseRecord>();
        while (r.Read())
        {
            list.Add(new VmwareReleaseRecord(
                r.GetString(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3)));
        }
        return list;
    }

    public int Count(string? sourceTag = null)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM build_lookup_cache" + (sourceTag is null ? ";" : " WHERE source = $s;");
        if (sourceTag is not null) cmd.Parameters.AddWithValue("$s", sourceTag);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void Clear(string sourceTag)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM build_lookup_cache WHERE source = $s;";
        cmd.Parameters.AddWithValue("$s", sourceTag);
        cmd.ExecuteNonQuery();
    }
}
