using System.Text.Json;
using DIG.Core.Models;

namespace DIG.Core.Data;

public sealed class WorkspaceRepository
{
    private readonly SqliteConnectionFactory _factory;
    public WorkspaceRepository(SqliteConnectionFactory factory) => _factory = factory;

    public long Upsert(long id, string name, IEnumerable<string> urls)
    {
        var json = JsonSerializer.Serialize(urls.ToList());
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        var now = DateTimeOffset.UtcNow.ToString("o");
        if (id > 0)
        {
            cmd.CommandText = "UPDATE workspaces SET name=$n, data_json=$d, updated_utc=$u WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", id);
        }
        else
        {
            cmd.CommandText = "INSERT INTO workspaces(name, data_json, updated_utc) VALUES($n, $d, $u); SELECT last_insert_rowid();";
        }
        cmd.Parameters.AddWithValue("$n", name);
        cmd.Parameters.AddWithValue("$d", json);
        cmd.Parameters.AddWithValue("$u", now);
        if (id > 0) { cmd.ExecuteNonQuery(); return id; }
        return (long)cmd.ExecuteScalar()!;
    }

    public IReadOnlyList<Workspace> GetAll()
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT id, name, data_json, updated_utc FROM workspaces ORDER BY updated_utc DESC;";
        using var r = cmd.ExecuteReader();
        var list = new List<Workspace>();
        while (r.Read())
        {
            IReadOnlyList<string> urls;
            try { urls = JsonSerializer.Deserialize<List<string>>(r.GetString(2)) ?? new List<string>(); }
            catch { urls = Array.Empty<string>(); }
            list.Add(new Workspace(r.GetInt64(0), r.GetString(1), urls, DateTimeOffset.Parse(r.GetString(3))));
        }
        return list;
    }

    public void Delete(long id)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM workspaces WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }
}
