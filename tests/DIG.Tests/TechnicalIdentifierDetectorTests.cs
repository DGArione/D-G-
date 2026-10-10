using DIG.Core.Intelligence;
using DIG.Core.Models;
using Xunit;

namespace DIG.Tests;

public class TechnicalIdentifierDetectorTests
{
    [Fact]
    public void Detects_IpV4()
    {
        var ids = TechnicalIdentifierDetector.Detect("Connect to 10.20.30.40 please");
        Assert.Contains(ids, i => i.Kind == IdentifierKind.IpV4 && i.Value == "10.20.30.40");
    }

    [Fact]
    public void Detects_BuildNumber_With_Prefix()
    {
        var ids = TechnicalIdentifierDetector.Detect("ESXi build 20513097 is installed");
        Assert.Contains(ids, i => i.Kind == IdentifierKind.VmwareBuildNumber && i.Value == "20513097");
    }

    [Fact]
    public void Detects_Fqdn()
    {
        var ids = TechnicalIdentifierDetector.Detect("open vcenter.lab.example.com now");
        Assert.Contains(ids, i => i.Kind == IdentifierKind.Fqdn && i.Value == "vcenter.lab.example.com");
    }

    [Fact]
    public void Detects_Url_And_Does_Not_Also_Report_Inner_Host()
    {
        var ids = TechnicalIdentifierDetector.Detect("see https://docs.example.com/notes");
        Assert.Contains(ids, i => i.Kind == IdentifierKind.Url);
        // The host inside the URL must not be double-reported as a separate FQDN.
        Assert.DoesNotContain(ids, i => i.Kind == IdentifierKind.Fqdn && i.Value == "docs.example.com");
    }

    [Fact]
    public void Detects_Kubernetes_Resource()
    {
        var ids = TechnicalIdentifierDetector.Detect("restart pod/my-app-7d8f9");
        Assert.Contains(ids, i => i.Kind == IdentifierKind.KubernetesResource);
    }

    [Fact]
    public void Detects_LogTimestamp()
    {
        var ids = TechnicalIdentifierDetector.Detect("error at 2026-10-10T12:34:56Z");
        Assert.Contains(ids, i => i.Kind == IdentifierKind.LogTimestamp);
    }

    [Fact]
    public void IsExactly_BuildNumber_True()
    {
        Assert.True(TechnicalIdentifierDetector.IsExactly("20513097", IdentifierKind.VmwareBuildNumber));
    }

    [Fact]
    public void Empty_Input_Returns_Empty()
    {
        Assert.Empty(TechnicalIdentifierDetector.Detect(null));
        Assert.Empty(TechnicalIdentifierDetector.Detect("   "));
    }
}
