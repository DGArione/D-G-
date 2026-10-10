using DIG.Core.Intelligence;
using DIG.Core.Models;
using Xunit;

namespace DIG.Tests;

public class VmwareBuildLookupTests
{
    private static VmwareBuildLookupService Service() =>
        new(new IBuildSource[] { new UnconfiguredAuthoritativeSource(), new LocalIndexBuildSource() });

    [Theory]
    [InlineData("build 20513097", "20513097")]
    [InlineData("Build# 20513097", "20513097")]
    [InlineData("  20,513,097 ", "20513097")]
    public void Normalizer_Strips_Prefix_And_Commas(string input, string expected)
    {
        Assert.Equal(expected, BuildQueryNormalizer.Normalize(input));
    }

    [Fact]
    public async Task Known_Build_Is_Possible_Match_From_Bundled_Index()
    {
        var result = await Service().LookupAsync("20513097");
        Assert.Equal(MatchConfidence.Possible, result.Confidence);
        Assert.Equal("VMware ESXi", result.Product);
        Assert.Contains("8.0", result.Version);
    }

    [Fact]
    public void Bundled_Index_Has_Many_Entries()
    {
        var index = new LocalIndexBuildSource();
        Assert.True(index.Count >= 30, $"expected a sizeable bundled index, got {index.Count}");
    }

    [Fact]
    public async Task Unknown_Build_Is_Unknown()
    {
        var result = await Service().LookupAsync("99999999");
        Assert.Equal(MatchConfidence.Unknown, result.Confidence);
        Assert.Null(result.Product);
    }

    [Fact]
    public async Task Empty_Query_Is_Unknown()
    {
        var result = await Service().LookupAsync("   ");
        Assert.Equal(MatchConfidence.Unknown, result.Confidence);
    }

    [Fact]
    public async Task Authoritative_Source_Yields_Verified()
    {
        var svc = new VmwareBuildLookupService(new IBuildSource[] { new FakeAuthoritative() });
        var result = await svc.LookupAsync("12345678");
        Assert.Equal(MatchConfidence.Verified, result.Confidence);
        Assert.Equal("2026-01-01", result.ReleaseDate);
    }

    private sealed class FakeAuthoritative : IBuildSource
    {
        public string Name => "Fake authoritative";
        public bool IsAuthoritative => true;
        public Task<IReadOnlyList<VmwareReleaseRecord>> QueryAsync(string q, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<VmwareReleaseRecord>>(new[]
            {
                new VmwareReleaseRecord("VMware NSX", "4.1", "12345678", "2026-01-01")
            });
    }
}
