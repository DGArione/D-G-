using DIG.Core.Ai;
using DIG.Core.Email;
using Xunit;

namespace DIG.Tests;

public class EmailTemplateRendererTests
{
    private static EmailTemplate Template(string subject, string body)
        => new(1, "T", "Response", subject, body, false, DateTimeOffset.UtcNow);

    [Fact]
    public void ExtractVariables_Finds_Unique_Names()
    {
        var vars = EmailTemplateRenderer.ExtractVariables("Hi {{customer}}, case {{case_number}} and {{customer}}");
        Assert.Equal(2, vars.Count);
        Assert.Contains("customer", vars);
        Assert.Contains("case_number", vars);
    }

    [Fact]
    public void Render_Substitutes_Provided_Values()
    {
        var t = Template("[{{case_number}}] Update", "Hello {{customer}}");
        var (subject, body) = EmailTemplateRenderer.Render(t, new Dictionary<string, string>
        {
            ["case_number"] = "12345",
            ["customer"] = "Acme"
        });
        Assert.Equal("[12345] Update", subject);
        Assert.Equal("Hello Acme", body);
    }

    [Fact]
    public void Render_Leaves_Missing_As_Bracketed_Placeholder()
    {
        var t = Template("x", "Hello {{customer}}");
        var (_, body) = EmailTemplateRenderer.Render(t, new Dictionary<string, string>());
        Assert.Equal("Hello [customer]", body);
    }

    [Fact]
    public void BuildEmailFromTemplate_Includes_Rendered_Draft_And_Review_Framing()
    {
        var t = Template("[{{case_number}}] Update", "Hello {{customer}}");
        var prompt = AiPrompts.BuildEmailFromTemplate(
            t,
            new Dictionary<string, string> { ["customer"] = "Acme", ["case_number"] = "999" },
            WritingTone.CustomerFriendly);

        Assert.Contains("Acme", prompt);
        Assert.Contains("999", prompt);
        Assert.Contains("Subject:", prompt);
    }

    [Fact]
    public void BuiltIn_Templates_Are_Present_And_Have_Variables()
    {
        Assert.NotEmpty(BuiltInEmailTemplates.All);
        var first = BuiltInEmailTemplates.All[0];
        Assert.NotEmpty(EmailTemplateRenderer.ExtractVariables(first));
    }
}
