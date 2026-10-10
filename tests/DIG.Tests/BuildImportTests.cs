using DIG.Core.Data;
using DIG.Core.Intelligence;
using DIG.Core.Models;
using Xunit;

namespace DIG.Tests;

public class BuildImportTests
{
    [Fact]
    public void BundledIndex_Loads_From_EmbeddedJson()
    {
        var records = VmwareBuildIndex.LoadBundled();
        Assert.NotEmpty(records);
        Assert.Contains(records, r => r.Build == "20513097" && r.Product == "VMware ESXi");
    }

    [Fact]
    public void Csv_With_Header_Parses()
    {
        const string csv = "product,version,build,releaseDate\n" +
                           "VMware ESXi,ESXi 8.0 U3,24022510,2024-06-25\n" +
                           "VMware vCenter Server,vCenter 8.0 U3,24022515,2024-06-25\n";
        var records = BuildCsvImporter.Parse(csv);
        Assert.Equal(2, records.Count);
        Assert.Contains(records, r => r.Build == "24022510" && r.Version.Contains("8.0 U3"));
    }

    [Fact]
    public void Csv_Positional_Without_Header_Parses()
    {
        const string csv = "VMware ESXi,ESXi 7.0 U3,18644231,2021-10-05\n";
        var records = BuildCsvImporter.Parse(csv);
        Assert.Single(records);
        Assert.Equal("18644231", records[0].Build);
    }

    [Fact]
    public void Csv_Skips_Rows_Without_Valid_Build()
    {
        const string csv = "product,version,build\nVMware ESXi,ESXi 8.0,notanumber\n";
        Assert.Empty(BuildCsvImporter.Parse(csv));
    }

    [Fact]
    public void TableParser_Extracts_Build_Version_Date()
    {
        var rows = new[]
        {
            new[] { "Version", "Build Number", "Release Date" },  // header row -> no build, skipped
            new[] { "ESXi 8.0 U2", "22380479", "2023-09-21" },
            new[] { "ESXi 8.0 U1", "21495797", "2023-04-18" },
        };
        var records = BroadcomTableParser.ParseRows(rows, "VMware ESXi");
        Assert.Equal(2, records.Count);
        Assert.Contains(records, r => r.Build == "22380479" && r.Product == "VMware ESXi");
    }

    [Fact]
    public void CacheRepository_RoundTrips_And_Serves_Via_Source()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"dig-build-{Guid.NewGuid():N}.db");
        try
        {
            var factory = new SqliteConnectionFactory(dbPath);
            using (var c = factory.Open()) DatabaseMigrator.Migrate(c);

            var repo = new BuildCacheRepository(factory);
            var n = repo.AddRange(new[]
            {
                new VmwareReleaseRecord("VMware NSX", "4.1.2", "22589036", "2023-11-14")
            }, "csv", "Possible");
            Assert.Equal(1, n);
            Assert.Equal(1, repo.Count("csv"));

            var source = new CachedBuildSource(repo, "csv", "csv", authoritative: false);
            var svc = new VmwareBuildLookupService(new IBuildSource[] { source });
            var result = svc.LookupAsync("22589036").GetAwaiter().GetResult();
            Assert.Equal(MatchConfidence.Possible, result.Confidence);
            Assert.Equal("VMware NSX", result.Product);

            repo.Clear("csv");
            Assert.Equal(0, repo.Count("csv"));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var f in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
                try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
    }
}
