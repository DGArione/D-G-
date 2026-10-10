using DIG.Core.Data;
using DIG.Core.Models;
using Xunit;

namespace DIG.Tests;

public class ModulesDataTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public ModulesDataTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dig-mod-{Guid.NewGuid():N}.db");
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
    public void Migration_V2_Adds_Workspaces()
    {
        using var c = _factory.Open();
        Assert.True(DatabaseMigrator.GetUserVersion(c) >= 2);
    }

    [Fact]
    public void Infrastructure_Upsert_Search_Delete()
    {
        var repo = new InfrastructureRepository(_factory);
        var id = repo.Upsert(new InfrastructureAsset(
            0, "esxi01", "10.1.2.3", "esxi01.lab.example.com", 443, "Production",
            "Acme", "VMware ESXi", "8.0", "Hypervisor", "vmware,prod", "mgmt host", "ssh", "root",
            DateTimeOffset.UtcNow));
        Assert.True(id > 0);

        Assert.Single(repo.Search("acme"));
        Assert.Single(repo.Search("10.1.2"));
        Assert.Single(repo.Search("esxi01.lab"));
        Assert.Empty(repo.Search("nonexistent-xyz"));

        var all = repo.Search();
        Assert.Single(all);
        Assert.Equal("esxi01", all[0].Name);
        Assert.Equal(443, all[0].Port);

        // update
        repo.Upsert(all[0] with { Environment = "Staging" });
        Assert.Single(repo.Search("staging"));

        repo.Delete(id);
        Assert.Empty(repo.Search());
    }

    [Fact]
    public void Workspace_Upsert_GetAll_Delete()
    {
        var repo = new WorkspaceRepository(_factory);
        var id = repo.Upsert(0, "VCF Upgrade", new[] { "https://a.com", "https://b.com" });
        Assert.True(id > 0);

        var all = repo.GetAll();
        Assert.Single(all);
        Assert.Equal("VCF Upgrade", all[0].Name);
        Assert.Equal(2, all[0].Urls.Count);
        Assert.Equal(2, all[0].TabCount);

        repo.Delete(id);
        Assert.Empty(repo.GetAll());
    }
}
