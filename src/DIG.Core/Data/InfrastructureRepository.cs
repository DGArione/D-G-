using DIG.Core.Models;
using Microsoft.Data.Sqlite;

namespace DIG.Core.Data;

/// <summary>CRUD + search over the infrastructure_assets table. Treats IPs/FQDNs as
/// potentially customer-sensitive metadata; stores no secrets.</summary>
public sealed class InfrastructureRepository
{
    private readonly SqliteConnectionFactory _factory;
    public InfrastructureRepository(SqliteConnectionFactory factory) => _factory = factory;

    public long Upsert(InfrastructureAsset a)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        var now = DateTimeOffset.UtcNow.ToString("o");
        if (a.Id > 0)
        {
            cmd.CommandText =
                "UPDATE infrastructure_assets SET name=$name, ip=$ip, fqdn=$fqdn, port=$port, environment=$env, " +
                "customer=$cust, product=$prod, version=$ver, role=$role, tags=$tags, notes=$notes, " +
                "connection=$conn, username_ref=$user, updated_utc=$upd WHERE id=$id;";
            cmd.Parameters.AddWithValue("$id", a.Id);
        }
        else
        {
            cmd.CommandText =
                "INSERT INTO infrastructure_assets(name, ip, fqdn, port, environment, customer, product, version, role, tags, notes, connection, username_ref, created_utc, updated_utc) " +
                "VALUES($name, $ip, $fqdn, $port, $env, $cust, $prod, $ver, $role, $tags, $notes, $conn, $user, $upd, $upd); SELECT last_insert_rowid();";
        }
        cmd.Parameters.AddWithValue("$name", a.Name);
        cmd.Parameters.AddWithValue("$ip", (object?)a.Ip ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fqdn", (object?)a.Fqdn ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$port", (object?)a.Port ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$env", (object?)a.Environment ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cust", (object?)a.Customer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$prod", (object?)a.Product ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ver", (object?)a.Version ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$role", (object?)a.Role ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$tags", (object?)a.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$notes", (object?)a.Notes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$conn", (object?)a.Connection ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$user", (object?)a.UsernameRef ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$upd", now);

        if (a.Id > 0) { cmd.ExecuteNonQuery(); return a.Id; }
        return (long)cmd.ExecuteScalar()!;
    }

    public void Delete(long id)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM infrastructure_assets WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<InfrastructureAsset> Search(string? term = null)
    {
        using var c = _factory.Open();
        using var cmd = c.CreateCommand();
        if (string.IsNullOrWhiteSpace(term))
        {
            cmd.CommandText = "SELECT * FROM infrastructure_assets ORDER BY name;";
        }
        else
        {
            cmd.CommandText =
                "SELECT * FROM infrastructure_assets WHERE " +
                "name LIKE $q OR ip LIKE $q OR fqdn LIKE $q OR customer LIKE $q OR " +
                "environment LIKE $q OR product LIKE $q OR role LIKE $q OR tags LIKE $q ORDER BY name;";
            cmd.Parameters.AddWithValue("$q", "%" + term.Trim() + "%");
        }

        using var r = cmd.ExecuteReader();
        var list = new List<InfrastructureAsset>();
        while (r.Read())
        {
            list.Add(Map(r));
        }
        return list;
    }

    private static InfrastructureAsset Map(SqliteDataReader r)
    {
        string? S(string col) { var i = r.GetOrdinal(col); return r.IsDBNull(i) ? null : r.GetString(i); }
        int? I(string col) { var i = r.GetOrdinal(col); return r.IsDBNull(i) ? null : r.GetInt32(i); }
        var updOrd = r.GetOrdinal("updated_utc");
        var upd = r.IsDBNull(updOrd) ? DateTimeOffset.UtcNow : DateTimeOffset.Parse(r.GetString(updOrd));
        return new InfrastructureAsset(
            r.GetInt64(r.GetOrdinal("id")), S("name") ?? "", S("ip"), S("fqdn"), I("port"),
            S("environment"), S("customer"), S("product"), S("version"), S("role"),
            S("tags"), S("notes"), S("connection"), S("username_ref"), upd);
    }
}
