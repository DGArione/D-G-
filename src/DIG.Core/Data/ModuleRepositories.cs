using DIG.Core.Models;
using Microsoft.Data.Sqlite;

namespace DIG.Core.Data;

public sealed class TaskRepository
{
    private readonly SqliteConnectionFactory _factory;
    public TaskRepository(SqliteConnectionFactory factory) => _factory = factory;

    public long Upsert(TaskItem t)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        var now = DateTimeOffset.UtcNow.ToString("o");
        if (t.Id > 0)
        {
            cmd.CommandText = "UPDATE tasks SET title=$t, done=$d, priority=$p, due_utc=$due, tags=$g, updated_utc=$u WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", t.Id);
        }
        else
        {
            cmd.CommandText = "INSERT INTO tasks(title, done, priority, due_utc, tags, created_utc, updated_utc) VALUES($t,$d,$p,$due,$g,$u,$u); SELECT last_insert_rowid();";
        }
        cmd.Parameters.AddWithValue("$t", t.Title);
        cmd.Parameters.AddWithValue("$d", t.Done ? 1 : 0);
        cmd.Parameters.AddWithValue("$p", (object?)t.Priority ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$due", (object?)t.DueUtc?.ToString("o") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$g", (object?)t.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$u", now);
        if (t.Id > 0) { cmd.ExecuteNonQuery(); return t.Id; }
        return (long)cmd.ExecuteScalar()!;
    }

    public void SetDone(long id, bool done)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE tasks SET done=$d, updated_utc=$u WHERE id=$id;";
        cmd.Parameters.AddWithValue("$d", done ? 1 : 0);
        cmd.Parameters.AddWithValue("$u", DateTimeOffset.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(long id) => Exec("DELETE FROM tasks WHERE id=$id;", id);

    public IReadOnlyList<TaskItem> GetAll()
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,title,done,priority,due_utc,tags,updated_utc FROM tasks ORDER BY done, COALESCE(due_utc,'9999'), id DESC;";
        using var r = cmd.ExecuteReader();
        var list = new List<TaskItem>();
        while (r.Read())
            list.Add(new TaskItem(r.GetInt64(0), r.GetString(1), r.GetInt64(2) != 0,
                r.IsDBNull(3) ? "Normal" : r.GetString(3),
                r.IsDBNull(4) ? null : DateTimeOffset.Parse(r.GetString(4)),
                r.IsDBNull(5) ? null : r.GetString(5),
                DateTimeOffset.Parse(r.GetString(6))));
        return list;
    }

    private void Exec(string sql, long id)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}

public sealed class NoteRepository
{
    private readonly SqliteConnectionFactory _factory;
    public NoteRepository(SqliteConnectionFactory factory) => _factory = factory;

    public long Upsert(NoteItem n)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        var now = DateTimeOffset.UtcNow.ToString("o");
        if (n.Id > 0)
        {
            cmd.CommandText = "UPDATE notes SET title=$t, body=$b, tags=$g, updated_utc=$u WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", n.Id);
        }
        else
        {
            cmd.CommandText = "INSERT INTO notes(title, body, tags, updated_utc) VALUES($t,$b,$g,$u); SELECT last_insert_rowid();";
        }
        cmd.Parameters.AddWithValue("$t", n.Title);
        cmd.Parameters.AddWithValue("$b", (object?)n.Body ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$g", (object?)n.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$u", now);
        if (n.Id > 0) { cmd.ExecuteNonQuery(); return n.Id; }
        return (long)cmd.ExecuteScalar()!;
    }

    public void Delete(long id)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM notes WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<NoteItem> GetAll()
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,title,body,tags,updated_utc FROM notes ORDER BY updated_utc DESC;";
        using var r = cmd.ExecuteReader();
        var list = new List<NoteItem>();
        while (r.Read())
            list.Add(new NoteItem(r.GetInt64(0), r.GetString(1),
                r.IsDBNull(2) ? "" : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                DateTimeOffset.Parse(r.GetString(4))));
        return list;
    }
}

public sealed class CalendarRepository
{
    private readonly SqliteConnectionFactory _factory;
    public CalendarRepository(SqliteConnectionFactory factory) => _factory = factory;

    public long Add(CalendarEvent e)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO calendar_events(title,start_utc,end_utc,kind,color,notes,created_utc) VALUES($t,$s,$e,$k,$c,$n,$cr); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$t", e.Title);
        cmd.Parameters.AddWithValue("$s", e.StartUtc.ToString("o"));
        cmd.Parameters.AddWithValue("$e", e.EndUtc.ToString("o"));
        cmd.Parameters.AddWithValue("$k", (object?)e.Kind ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$c", (object?)e.Color ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$n", (object?)e.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cr", DateTimeOffset.UtcNow.ToString("o"));
        return (long)cmd.ExecuteScalar()!;
    }

    public void Delete(long id)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM calendar_events WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<CalendarEvent> GetUpcoming(int limit = 100)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,title,start_utc,end_utc,kind,color,notes FROM calendar_events ORDER BY start_utc LIMIT $n;";
        cmd.Parameters.AddWithValue("$n", limit);
        using var r = cmd.ExecuteReader();
        var list = new List<CalendarEvent>();
        while (r.Read())
            list.Add(new CalendarEvent(r.GetInt64(0), r.GetString(1),
                DateTimeOffset.Parse(r.GetString(2)), DateTimeOffset.Parse(r.GetString(3)),
                r.IsDBNull(4) ? "Event" : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6)));
        return list;
    }
}

public sealed class SshProfileRepository
{
    private readonly SqliteConnectionFactory _factory;
    public SshProfileRepository(SqliteConnectionFactory factory) => _factory = factory;

    public long Upsert(SshProfile p)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        var now = DateTimeOffset.UtcNow.ToString("o");
        if (p.Id > 0)
        {
            cmd.CommandText = "UPDATE ssh_profiles SET name=$n, host=$h, port=$po, username=$u, kind=$k, notes=$no, updated_utc=$up WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", p.Id);
        }
        else
        {
            cmd.CommandText = "INSERT INTO ssh_profiles(name,host,port,username,kind,notes,updated_utc) VALUES($n,$h,$po,$u,$k,$no,$up); SELECT last_insert_rowid();";
        }
        cmd.Parameters.AddWithValue("$n", p.Name);
        cmd.Parameters.AddWithValue("$h", p.Host);
        cmd.Parameters.AddWithValue("$po", p.Port);
        cmd.Parameters.AddWithValue("$u", (object?)p.Username ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$k", p.Kind);
        cmd.Parameters.AddWithValue("$no", (object?)p.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$up", now);
        if (p.Id > 0) { cmd.ExecuteNonQuery(); return p.Id; }
        return (long)cmd.ExecuteScalar()!;
    }

    public void Delete(long id)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM ssh_profiles WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<SshProfile> GetAll()
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id,name,host,port,username,kind,notes,updated_utc FROM ssh_profiles ORDER BY name;";
        using var r = cmd.ExecuteReader();
        var list = new List<SshProfile>();
        while (r.Read())
            list.Add(new SshProfile(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt32(3),
                r.IsDBNull(4) ? null : r.GetString(4), r.IsDBNull(5) ? "ssh" : r.GetString(5),
                r.IsDBNull(6) ? null : r.GetString(6), DateTimeOffset.Parse(r.GetString(7))));
        return list;
    }
}

public sealed class KbRepository
{
    private readonly SqliteConnectionFactory _factory;
    public KbRepository(SqliteConnectionFactory factory) => _factory = factory;

    public long Upsert(KbEntry e)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        var now = DateTimeOffset.UtcNow.ToString("o");
        if (e.Id > 0)
        {
            cmd.CommandText = "UPDATE kb_entries SET title=$t, content=$c, source=$s, tags=$g, updated_utc=$u WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", e.Id);
        }
        else
        {
            cmd.CommandText = "INSERT INTO kb_entries(title,content,source,tags,updated_utc) VALUES($t,$c,$s,$g,$u); SELECT last_insert_rowid();";
        }
        cmd.Parameters.AddWithValue("$t", e.Title);
        cmd.Parameters.AddWithValue("$c", (object?)e.Content ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$s", (object?)e.Source ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$g", (object?)e.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$u", now);
        if (e.Id > 0) { cmd.ExecuteNonQuery(); return e.Id; }
        return (long)cmd.ExecuteScalar()!;
    }

    public void Delete(long id)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM kb_entries WHERE id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<KbEntry> Search(string? term)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        if (string.IsNullOrWhiteSpace(term))
            cmd.CommandText = "SELECT id,title,content,source,tags,updated_utc FROM kb_entries ORDER BY updated_utc DESC;";
        else
        {
            cmd.CommandText = "SELECT id,title,content,source,tags,updated_utc FROM kb_entries WHERE title LIKE $q OR content LIKE $q OR tags LIKE $q ORDER BY updated_utc DESC;";
            cmd.Parameters.AddWithValue("$q", "%" + term.Trim() + "%");
        }
        using var r = cmd.ExecuteReader();
        var list = new List<KbEntry>();
        while (r.Read())
            list.Add(new KbEntry(r.GetInt64(0), r.GetString(1),
                r.IsDBNull(2) ? "" : r.GetString(2),
                r.IsDBNull(3) ? null : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                DateTimeOffset.Parse(r.GetString(5))));
        return list;
    }
}
