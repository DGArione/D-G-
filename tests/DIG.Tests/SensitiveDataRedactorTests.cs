using DIG.Core.Security;
using Xunit;

namespace DIG.Tests;

public class SensitiveDataRedactorTests
{
    [Fact]
    public void Redacts_Google_Api_Key()
    {
        var input = "key is AIzaSyA1234567890abcdefghijklmnopqrstuvw done";
        var r = SensitiveDataRedactor.Redact(input);
        Assert.DoesNotContain("AIzaSy", r.Text);
        Assert.True(r.AnyRedacted);
    }

    [Fact]
    public void Redacts_Password_Assignment()
    {
        var r = SensitiveDataRedactor.Redact("password=Sup3rSecret!");
        Assert.DoesNotContain("Sup3rSecret", r.Text);
        Assert.Contains("[REDACTED]", r.Text);
    }

    [Fact]
    public void Redacts_Url_Credentials()
    {
        var r = SensitiveDataRedactor.Redact("https://admin:hunter2@vcenter.example.com");
        Assert.DoesNotContain("hunter2", r.Text);
        Assert.Contains("admin:[REDACTED]@", r.Text);
    }

    [Fact]
    public void Redacts_Private_Key_Block()
    {
        var input = "-----BEGIN RSA PRIVATE KEY-----\nMIIabc\n-----END RSA PRIVATE KEY-----";
        var r = SensitiveDataRedactor.Redact(input);
        Assert.DoesNotContain("MIIabc", r.Text);
    }

    [Fact]
    public void Network_Identifiers_Only_When_Enabled()
    {
        const string input = "host vcenter.example.com at 10.1.2.3";
        var off = SensitiveDataRedactor.Redact(input);
        Assert.Contains("10.1.2.3", off.Text);

        var on = SensitiveDataRedactor.Redact(input, new RedactionOptions(RedactNetworkIdentifiers: true));
        Assert.DoesNotContain("10.1.2.3", on.Text);
        Assert.DoesNotContain("vcenter.example.com", on.Text);
    }

    [Fact]
    public void Clean_Text_Is_Unchanged()
    {
        const string input = "This text has nothing sensitive in it.";
        var r = SensitiveDataRedactor.Redact(input);
        Assert.Equal(input, r.Text);
        Assert.False(r.AnyRedacted);
    }
}
