using DIG.Core.Data;
using DIG.Core.Models;
using DIG.Core.Security;
using Xunit;

namespace DIG.Tests;

public class BlocklistTests
{
    [Theory]
    [InlineData("doubleclick.net", true)]
    [InlineData("ad.doubleclick.net", true)]
    [InlineData("www.google-analytics.com", true)]
    [InlineData("example.com", false)]
    [InlineData("notgoogle-analytics.com.evil", false)]
    [InlineData("", false)]
    public void IsBlocked_Works(string host, bool expected)
        => Assert.Equal(expected, TrackerBlocklist.IsBlocked(host));

    [Fact]
    public void HostOf_Extracts_Host()
        => Assert.Equal("ads.example.com", TrackerBlocklist.HostOf("https://ads.example.com/x?y=1"));
}

public class Push2DataTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public Push2DataTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dig-p2-{Guid.NewGuid():N}.db");
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
    public void Migration_V3_Present()
    {
        using var c = _factory.Open();
        Assert.True(DatabaseMigrator.GetUserVersion(c) >= 3);
    }

    [Fact]
    public void Tasks_RoundTrip()
    {
        var repo = new TaskRepository(_factory);
        var id = repo.Upsert(new TaskItem(0, "Patch ESXi", false, "High", null, null, DateTimeOffset.UtcNow));
        Assert.True(id > 0);
        repo.SetDone(id, true);
        Assert.True(repo.GetAll().First(t => t.Id == id).Done);
        repo.Delete(id);
        Assert.DoesNotContain(repo.GetAll(), t => t.Id == id);
    }

    [Fact]
    public void Notes_RoundTrip()
    {
        var repo = new NoteRepository(_factory);
        var id = repo.Upsert(new NoteItem(0, "Runbook", "steps...", null, DateTimeOffset.UtcNow));
        Assert.Single(repo.GetAll());
        repo.Delete(id);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Calendar_RoundTrip()
    {
        var repo = new CalendarRepository(_factory);
        var now = DateTimeOffset.UtcNow;
        var id = repo.Add(new CalendarEvent(0, "On-call", now, now.AddHours(8), "Shift", null, null));
        Assert.Single(repo.GetUpcoming());
        repo.Delete(id);
        Assert.Empty(repo.GetUpcoming());
    }

    [Fact]
    public void Ssh_RoundTrip()
    {
        var repo = new SshProfileRepository(_factory);
        var id = repo.Upsert(new SshProfile(0, "esxi01", "10.1.2.3", 22, "root", "ssh", null, DateTimeOffset.UtcNow));
        var all = repo.GetAll();
        Assert.Single(all);
        Assert.Equal("root@10.1.2.3:22", all[0].Target);
        repo.Delete(id);
        Assert.Empty(repo.GetAll());
    }

    [Fact]
    public void Kb_Search_RoundTrip()
    {
        var repo = new KbRepository(_factory);
        repo.Upsert(new KbEntry(0, "PSOD causes", "purple screen of death notes", "kb", "esxi,psod", DateTimeOffset.UtcNow));
        Assert.Single(repo.Search("purple"));
        Assert.Single(repo.Search("psod"));
        Assert.Empty(repo.Search("nonexistent-term-xyz"));
    }
}
