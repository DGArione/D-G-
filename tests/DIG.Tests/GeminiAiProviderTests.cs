using System.Net;
using System.Text;
using System.Text.Json;
using DIG.Core.Ai;
using Xunit;

namespace DIG.Tests;

public class GeminiAiProviderTests
{
    [Fact]
    public void BuildPayload_Includes_Contents_And_SystemInstruction()
    {
        var req = new AiCompletionRequest
        {
            Model = "gemini-1.5-flash",
            ApiKey = "k",
            SystemInstruction = "be nice",
            Temperature = 0.3,
            MaxOutputTokens = 256,
            Messages = new[] { new AiMessage(AiRole.User, "hello") }
        };

        var json = GeminiAiProvider.BuildPayload(req);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("contents", out var contents));
        Assert.Equal("user", contents[0].GetProperty("role").GetString());
        Assert.Equal("hello", contents[0].GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal("be nice", root.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString());
        Assert.Equal(0.3, root.GetProperty("generationConfig").GetProperty("temperature").GetDouble(), 3);
    }

    [Fact]
    public void ParseCandidates_Extracts_Text()
    {
        const string body = """
        { "candidates": [ { "content": { "parts": [ { "text": "Hi there" } ] }, "finishReason": "STOP" } ] }
        """;
        var (text, error) = GeminiAiProvider.ParseCandidates(body);
        Assert.Null(error);
        Assert.Equal("Hi there", text);
    }

    [Fact]
    public void ParseCandidates_Handles_SafetyBlock()
    {
        const string body = """{ "promptFeedback": { "blockReason": "SAFETY" } }""";
        var (_, error) = GeminiAiProvider.ParseCandidates(body);
        Assert.NotNull(error);
        Assert.Contains("SAFETY", error);
    }

    [Fact]
    public async Task CompleteAsync_Returns_Text_On_Success()
    {
        const string body = """
        { "candidates": [ { "content": { "parts": [ { "text": "Draft email ready" } ] }, "finishReason": "STOP" } ] }
        """;
        var http = new HttpClient(new StubHandler(HttpStatusCode.OK, body));
        var provider = new GeminiAiProvider(http);

        var result = await provider.CompleteAsync(new AiCompletionRequest
        {
            Model = "gemini-1.5-flash",
            ApiKey = "test-key",
            Messages = new[] { new AiMessage(AiRole.User, "draft") }
        });

        Assert.True(result.Success);
        Assert.Equal("Draft email ready", result.Text);
    }

    [Fact]
    public async Task CompleteAsync_Surfaces_Api_Error()
    {
        const string body = """{ "error": { "code": 400, "message": "API key not valid" } }""";
        var http = new HttpClient(new StubHandler(HttpStatusCode.BadRequest, body));
        var provider = new GeminiAiProvider(http);

        var result = await provider.CompleteAsync(new AiCompletionRequest
        {
            Model = "gemini-1.5-flash",
            ApiKey = "bad",
            Messages = new[] { new AiMessage(AiRole.User, "x") }
        });

        Assert.False(result.Success);
        Assert.Contains("API key not valid", result.Error);
    }

    [Fact]
    public async Task CompleteAsync_Fails_Without_ApiKey()
    {
        var provider = new GeminiAiProvider(new HttpClient(new StubHandler(HttpStatusCode.OK, "{}")));
        var result = await provider.CompleteAsync(new AiCompletionRequest
        {
            Model = "gemini-1.5-flash",
            ApiKey = "",
            Messages = new[] { new AiMessage(AiRole.User, "x") }
        });
        Assert.False(result.Success);
        Assert.Contains("API key", result.Error);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        public StubHandler(HttpStatusCode status, string body) { _status = status; _body = body; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
    }
}
