using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DIG.Core.Ai;

/// <summary>
/// Google Gemini provider using the Generative Language REST API
/// (models/{model}:generateContent). Stateless apart from its HttpClient;
/// the API key is supplied per request and is never logged.
/// </summary>
public sealed class GeminiAiProvider : IAiProvider
{
    public const string DefaultBaseUrl = "https://generativelanguage.googleapis.com/v1beta";

    public string Id => "gemini";
    public string DisplayName => "Google Gemini";

    private readonly HttpClient _http;
    private readonly ILogger<GeminiAiProvider> _logger;

    public GeminiAiProvider(HttpClient http, ILogger<GeminiAiProvider>? logger = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? NullLogger<GeminiAiProvider>.Instance;
    }

    public async Task<AiResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return AiResult.Fail("No Gemini API key configured. Add one in Settings → AI.");
        if (string.IsNullOrWhiteSpace(request.Model))
            return AiResult.Fail("No Gemini model selected.");

        var baseUrl = (request.BaseUrlOverride ?? DefaultBaseUrl).TrimEnd('/');
        var url = $"{baseUrl}/models/{Uri.EscapeDataString(request.Model)}:generateContent?key={Uri.EscapeDataString(request.ApiKey)}";

        var payload = BuildPayload(request);

        try
        {
            using var httpReq = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(httpReq, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                var apiError = TryExtractError(body) ?? $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
                _logger.LogWarning("Gemini request failed ({Model}): {Status}", request.Model, resp.StatusCode);
                return AiResult.Fail(apiError);
            }

            var (text, error) = ParseCandidates(body);
            if (error is not null)
                return AiResult.Fail(error);

            return AiResult.Ok(text, request.Model);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini request threw for model {Model}", request.Model);
            return AiResult.Fail($"Request failed: {ex.Message}");
        }
    }

    /// <summary>Serialises the request into the Gemini JSON body. Internal for testing.</summary>
    internal static string BuildPayload(AiCompletionRequest request)
    {
        var contents = new List<object>();
        foreach (var m in request.Messages)
        {
            contents.Add(new
            {
                role = m.Role == AiRole.User ? "user" : "model",
                parts = new[] { new { text = m.Text } }
            });
        }

        var genConfig = new Dictionary<string, object>();
        if (request.Temperature is { } t) genConfig["temperature"] = t;
        if (request.MaxOutputTokens is { } max) genConfig["maxOutputTokens"] = max;

        var root = new Dictionary<string, object>
        {
            ["contents"] = contents
        };
        if (!string.IsNullOrWhiteSpace(request.SystemInstruction))
        {
            root["systemInstruction"] = new { parts = new[] { new { text = request.SystemInstruction } } };
        }
        if (genConfig.Count > 0)
        {
            root["generationConfig"] = genConfig;
        }

        return JsonSerializer.Serialize(root);
    }

    internal static (string text, string? error) ParseCandidates(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var rootEl = doc.RootElement;

            if (rootEl.TryGetProperty("promptFeedback", out var feedback)
                && feedback.TryGetProperty("blockReason", out var blockReason))
            {
                return (string.Empty, $"Request blocked by safety filter: {blockReason.GetString()}");
            }

            if (!rootEl.TryGetProperty("candidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0)
            {
                return (string.Empty, "Model returned no candidates.");
            }

            var first = candidates[0];
            if (first.TryGetProperty("finishReason", out var fr)
                && fr.GetString() is { } reason
                && reason is not ("STOP" or "MAX_TOKENS"))
            {
                return (string.Empty, $"Generation stopped: {reason}");
            }

            var sb = new StringBuilder();
            if (first.TryGetProperty("content", out var content)
                && content.TryGetProperty("parts", out var parts)
                && parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textEl))
                        sb.Append(textEl.GetString());
                }
            }

            var text = sb.ToString();
            return text.Length == 0
                ? (string.Empty, "Model returned an empty response.")
                : (text, null);
        }
        catch (JsonException)
        {
            return (string.Empty, "Could not parse the model response.");
        }
    }

    private static string? TryExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err)
                && err.TryGetProperty("message", out var msg))
            {
                return msg.GetString();
            }
        }
        catch (JsonException)
        {
            // fall through
        }
        return null;
    }
}
