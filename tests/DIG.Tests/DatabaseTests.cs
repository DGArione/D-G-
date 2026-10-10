using DIG.Core.Data;
using Xunit;

namespace DIG.Tests;

public class DatabaseTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public DatabaseTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dig-test-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_dbPath);
        using var c = _factory.Open();
        DatabaseMigrator.Migrate(c);
    }

    public void Dispose()
    {
        try { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); } catch { }
        foreach (var f in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    [Fact]
    public void Migrate_Sets_User_Version_To_Latest()
    {
        using var c = _factory.Open();
        Assert.Equal(DatabaseMigrator.Migrations.Count, DatabaseMigrator.GetUserVersion(c));
    }

    [Fact]
    public void Migrate_Is_Idempotent()
    {
        using var c = _factory.Open();
        var v = DatabaseMigrator.Migrate(c); // run again
        Assert.Equal(DatabaseMigrator.Migrations.Count, v);
    }

    [Fact]
    public void Settings_RoundTrip()
    {
        var repo = new SettingsRepository(_factory);
        repo.Set("search_engine", "Brave");
        Assert.Equal("Brave", repo.Get("search_engine"));
        Assert.Equal("fallback", repo.Get("missing", "fallback"));

        repo.Set("search_engine", "Google"); // upsert
        Assert.Equal("Google", repo.Get("search_engine"));
    }

    [Fact]
    public void Bookmarks_Add_Get_Delete()
    {
        var repo = new BookmarkRepository(_factory);
        var id = repo.Add("Broadcom", "https://broadcom.com", "VMware", "docs");
        Assert.Single(repo.GetAll());
        repo.Delete(id);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void History_Add_Recent_Clear()
    {
        var repo = new HistoryRepository(_factory);
        repo.Add("https://a.com", "A");
        repo.Add("https://b.com", "B");
        Assert.Equal(2, repo.GetRecent().Count);
        repo.Clear();
        Assert.Empty(repo.GetRecent());
    }

    [Fact]
    public void EmailTemplates_Seed_Upsert_Delete()
    {
        var repo = new EmailTemplateRepository(_factory);
        var seeded = repo.SeedBuiltInsIfEmpty();
        Assert.True(seeded > 0);
        // Seeding again is a no-op because the table is not empty.
        Assert.Equal(0, repo.SeedBuiltInsIfEmpty());

        var all = repo.GetAll();
        Assert.NotEmpty(all);

        var newId = repo.Upsert(new DIG.Core.Email.EmailTemplate(
            0, "Custom", "Response", "Subj {{customer}}", "Body", false, DateTimeOffset.UtcNow));
        Assert.True(newId > 0);

        repo.Delete(newId);
        Assert.DoesNotContain(repo.GetAll(), t => t.Id == newId);
    }
}
