using DIG.Core.Intelligence;
using DIG.Core.Models;
using Xunit;

namespace DIG.Tests;

public class OmniboxInterpreterTests
{
    [Theory]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("http://example.com/path", "http://example.com/path")]
    [InlineData("about:blank", "about:blank")]
    public void Explicit_Scheme_Navigates_Unchanged(string input, string expected)
    {
        var r = OmniboxInterpreter.Interpret(input, SearchEngine.Google);
        Assert.Equal(OmniboxActionKind.NavigateUrl, r.Action);
        Assert.Equal(expected, r.TargetUrl);
    }

    [Fact]
    public void Bare_Domain_Navigates_Https()
    {
        var r = OmniboxInterpreter.Interpret("broadcom.com", SearchEngine.Google);
        Assert.Equal(OmniboxActionKind.NavigateUrl, r.Action);
        Assert.Equal("https://broadcom.com", r.TargetUrl);
    }

    [Fact]
    public void Bare_Ip_Navigates_Http()
    {
        var r = OmniboxInterpreter.Interpret("192.168.1.10", SearchEngine.Google);
        Assert.Equal(OmniboxActionKind.NavigateUrl, r.Action);
        Assert.Equal("http://192.168.1.10", r.TargetUrl);
    }

    [Fact]
    public void Localhost_With_Port_Navigates_Http()
    {
        var r = OmniboxInterpreter.Interpret("localhost:8080", SearchEngine.Google);
        Assert.Equal(OmniboxActionKind.NavigateUrl, r.Action);
        Assert.Equal("http://localhost:8080", r.TargetUrl);
    }

    [Fact]
    public void Multi_Word_Is_Search()
    {
        var r = OmniboxInterpreter.Interpret("vmware esxi purple screen", SearchEngine.DuckDuckGo);
        Assert.Equal(OmniboxActionKind.Search, r.Action);
        Assert.StartsWith("https://duckduckgo.com/?q=", r.TargetUrl);
    }

    [Fact]
    public void Single_Word_No_Dot_Is_Search()
    {
        var r = OmniboxInterpreter.Interpret("vmware", SearchEngine.Brave);
        Assert.Equal(OmniboxActionKind.Search, r.Action);
        Assert.StartsWith("https://search.brave.com/search?q=", r.TargetUrl);
    }

    [Fact]
    public void Build_Number_Is_Search_But_Surfaces_Identifier()
    {
        var r = OmniboxInterpreter.Interpret("20513097", SearchEngine.Google);
        Assert.Equal(OmniboxActionKind.Search, r.Action);
        Assert.Contains(r.Identifiers, i => i.Kind == IdentifierKind.VmwareBuildNumber);
    }
}
