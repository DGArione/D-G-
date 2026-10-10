using Microsoft.Data.Sqlite;

namespace DIG.Core.Data;

/// <summary>Creates and opens connections to the application's SQLite database.</summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public string DatabasePath { get; }

    public SqliteConnectionFactory(string databasePath)
    {
        DatabasePath = databasePath;
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var pragma = conn.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
        pragma.ExecuteNonQuery();
        return conn;
    }
}

/// <summary>
/// Applies versioned schema migrations tracked via SQLite's PRAGMA user_version.
/// Each entry is a full migration run inside a transaction; adding a new schema
/// change means appending one string to <see cref="Migrations"/>.
/// </summary>
public static class DatabaseMigrator
{
    public static readonly IReadOnlyList<string> Migrations = new[]
    {
        // v1 — initial schema
        """
        CREATE TABLE IF NOT EXISTS settings (
            key   TEXT PRIMARY KEY,
            value TEXT
        );

        CREATE TABLE IF NOT EXISTS bookmarks (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            title       TEXT NOT NULL,
            url         TEXT NOT NULL,
            folder      TEXT,
            tags        TEXT,
            created_utc TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_bookmarks_url ON bookmarks(url);

        CREATE TABLE IF NOT EXISTS history (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            url         TEXT NOT NULL,
            title       TEXT,
            visited_utc TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_history_visited ON history(visited_utc DESC);

        CREATE TABLE IF NOT EXISTS email_templates (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            name        TEXT NOT NULL,
            category    TEXT NOT NULL,
            subject     TEXT NOT NULL,
            body        TEXT NOT NULL,
            is_builtin  INTEGER NOT NULL DEFAULT 0,
            updated_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS build_lookup_cache (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            query         TEXT NOT NULL,
            product       TEXT,
            version       TEXT,
            build         TEXT,
            release_date  TEXT,
            source        TEXT,
            source_url    TEXT,
            confidence    TEXT,
            retrieved_utc TEXT NOT NULL,
            pinned        INTEGER NOT NULL DEFAULT 0
        );
        CREATE INDEX IF NOT EXISTS ix_build_cache_query ON build_lookup_cache(query);

        CREATE TABLE IF NOT EXISTS infrastructure_assets (
            id               INTEGER PRIMARY KEY AUTOINCREMENT,
            name             TEXT NOT NULL,
            ip               TEXT,
            fqdn             TEXT,
            port             INTEGER,
            environment      TEXT,
            customer         TEXT,
            product          TEXT,
            version          TEXT,
            role             TEXT,
            tags             TEXT,
            notes            TEXT,
            connection       TEXT,
            username_ref     TEXT,
            secret_ref       TEXT,
            created_utc      TEXT NOT NULL,
            updated_utc      TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS audit_events (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            action      TEXT NOT NULL,
            detail      TEXT,
            created_utc TEXT NOT NULL
        );
        """,
    };

    /// <summary>Migrates the connection to the latest version; returns the new version.</summary>
    public static int Migrate(SqliteConnection connection)
    {
        var current = GetUserVersion(connection);
        for (var i = current; i < Migrations.Count; i++)
        {
            using var tx = connection.BeginTransaction();
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = Migrations[i];
                cmd.ExecuteNonQuery();
            }
            using (var setVer = connection.CreateCommand())
            {
                setVer.Transaction = tx;
                // user_version does not accept parameters; value is an int we control.
                setVer.CommandText = $"PRAGMA user_version = {i + 1};";
                setVer.ExecuteNonQuery();
            }
            tx.Commit();
        }
        return Migrations.Count;
    }

    public static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        var result = cmd.ExecuteScalar();
        return Convert.ToInt32(result);
    }
}
