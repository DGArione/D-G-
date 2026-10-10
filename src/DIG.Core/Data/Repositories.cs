using DIG.Core.Email;
using DIG.Core.Models;
using Microsoft.Data.Sqlite;

namespace DIG.Core.Data;

/// <summary>Key/value application settings (non-secret values only).</summary>
public sealed class SettingsRepository
{
    private readonly SqliteConnectionFactory _factory;
    public SettingsRepository(SqliteConnectionFactory factory) => _factory = factory;

    public string? Get(string key)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $k;";
        cmd.Parameters.AddWithValue("$k", key);
        return cmd.ExecuteScalar() as string;
    }

    public string Get(string key, string fallback) => Get(key) ?? fallback;

    public void Set(string key, string? value)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "INSERT INTO settings(key, value) VALUES($k, $v) " +
            "ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", (object?)value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyDictionary<string, string> GetAll()
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (!r.IsDBNull(1)) dict[r.GetString(0)] = r.GetString(1);
        }
        return dict;
    }
}

public sealed class BookmarkRepository
{
    private readonly SqliteConnectionFactory _factory;
    public BookmarkRepository(SqliteConnectionFactory factory) => _factory = factory;

    public long Add(string title, string url, string? folder = null, string? tags = null)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "INSERT INTO bookmarks(title, url, folder, tags, created_utc) " +
            "VALUES($t, $u, $f, $g, $c); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$u", url);
        cmd.Parameters.AddWithValue("$f", (object?)folder ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$g", (object?)tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$c", DateTimeOffset.UtcNow.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    public IReadOnlyList<Bookmark> GetAll()
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, title, url, folder, tags, created_utc FROM bookmarks ORDER BY created_utc DESC;";
        using var r = cmd.ExecuteReader();
        var list = new List<Bookmark>();
        while (r.Read())
        {
            list.Add(new Bookmark(
                r.GetInt64(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                DateTimeOffset.Parse(r.GetString(5))));
        }
        return list;
    }

    public void Delete(long id)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM bookmarks WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}

public sealed class HistoryRepository
{
    private readonly SqliteConnectionFactory _factory;
    public HistoryRepository(SqliteConnectionFactory factory) => _factory = factory;

    public void Add(string url, string? title)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO history(url, title, visited_utc) VALUES($u, $t, $v);";
        cmd.Parameters.AddWithValue("$u", url);
        cmd.Parameters.AddWithValue("$t", (object?)title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$v", DateTimeOffset.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<HistoryEntry> GetRecent(int limit = 200)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, url, title, visited_utc FROM history ORDER BY visited_utc DESC LIMIT $n;";
        cmd.Parameters.AddWithValue("$n", limit);
        using var r = cmd.ExecuteReader();
        var list = new List<HistoryEntry>();
        while (r.Read())
        {
            list.Add(new HistoryEntry(
                r.GetInt64(0), r.GetString(1),
                r.IsDBNull(2) ? null : r.GetString(2),
                DateTimeOffset.Parse(r.GetString(3))));
        }
        return list;
    }

    public void Clear()
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM history;";
        cmd.ExecuteNonQuery();
    }
}

public sealed class EmailTemplateRepository
{
    private readonly SqliteConnectionFactory _factory;
    public EmailTemplateRepository(SqliteConnectionFactory factory) => _factory = factory;

    public IReadOnlyList<EmailTemplate> GetAll()
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText =
            "SELECT id, name, category, subject, body, is_builtin, updated_utc " +
            "FROM email_templates ORDER BY category, name;";
        using var r = cmd.ExecuteReader();
        var list = new List<EmailTemplate>();
        while (r.Read())
        {
            list.Add(new EmailTemplate(
                r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                r.GetInt64(5) != 0, DateTimeOffset.Parse(r.GetString(6))));
        }
        return list;
    }

    public long Upsert(EmailTemplate template)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        if (template.Id > 0)
        {
            cmd.CommandText =
                "UPDATE email_templates SET name=$n, category=$c, subject=$s, body=$b, updated_utc=$u WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", template.Id);
        }
        else
        {
            cmd.CommandText =
                "INSERT INTO email_templates(name, category, subject, body, is_builtin, updated_utc) " +
                "VALUES($n, $c, $s, $b, $ib, $u); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$ib", template.IsBuiltIn ? 1 : 0);
        }
        cmd.Parameters.AddWithValue("$n", template.Name);
        cmd.Parameters.AddWithValue("$c", template.Category);
        cmd.Parameters.AddWithValue("$s", template.Subject);
        cmd.Parameters.AddWithValue("$b", template.Body);
        cmd.Parameters.AddWithValue("$u", DateTimeOffset.UtcNow.ToString("o"));

        if (template.Id > 0)
        {
            cmd.ExecuteNonQuery();
            return template.Id;
        }
        return (long)cmd.ExecuteScalar()!;
    }

    public void Delete(long id)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM email_templates WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Inserts the built-in templates if the table is currently empty.</summary>
    public int SeedBuiltInsIfEmpty()
    {
        using var c = _factory.Open();
        using (var count = c.CreateCommand())
        {
            count.CommandText = "SELECT COUNT(*) FROM email_templates;";
            if (Convert.ToInt32(count.ExecuteScalar()) > 0) return 0;
        }

        var inserted = 0;
        using var tx = c.BeginTransaction();
        foreach (var t in BuiltInEmailTemplates.All)
        {
            using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText =
                "INSERT INTO email_templates(name, category, subject, body, is_builtin, updated_utc) " +
                "VALUES($n, $c, $s, $b, 1, $u);";
            cmd.Parameters.AddWithValue("$n", t.Name);
            cmd.Parameters.AddWithValue("$c", t.Category);
            cmd.Parameters.AddWithValue("$s", t.Subject);
            cmd.Parameters.AddWithValue("$b", t.Body);
            cmd.Parameters.AddWithValue("$u", DateTimeOffset.UtcNow.ToString("o"));
            cmd.ExecuteNonQuery();
            inserted++;
        }
        tx.Commit();
        return inserted;
    }
}
