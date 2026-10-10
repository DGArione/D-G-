using DIG.Core.Ai;
using DIG.Core.Email;
using DIG.Core.Security;

namespace DIG.App.Services;

/// <summary>
/// High-level AI orchestration: resolves the key/model from settings, applies
/// redaction before anything leaves the machine, and calls the provider.
/// </summary>
public sealed class AiService
{
    private readonly IAiProvider _provider;
    private readonly AppSettingsService _settings;

    public AiService(IAiProvider provider, AppSettingsService settings)
    {
        _provider = provider;
        _settings = settings;
    }

    public string ProviderName => _provider.DisplayName;
    public bool IsConfigured => _settings.AiEnabled && _settings.HasGeminiApiKey;

    /// <summary>Shows exactly what text will be sent after redaction, for user consent.</summary>
    public RedactionResult PreviewOutgoing(string text)
        => SensitiveDataRedactor.Redact(text, new RedactionOptions(_settings.RedactNetworkBeforeAi));

    public async Task<AiResult> SendAsync(
        IReadOnlyList<AiMessage> history,
        string? systemInstruction = null,
        CancellationToken ct = default)
    {
        if (!_settings.AiEnabled)
            return AiResult.Fail("AI features are disabled in Settings.");

        var key = _settings.GetGeminiApiKey();
        if (string.IsNullOrWhiteSpace(key))
            return AiResult.Fail("No Gemini API key configured. Add one in Settings → AI.");

        var options = new RedactionOptions(_settings.RedactNetworkBeforeAi);
        var redacted = history
            .Select(m => m.Role == AiRole.User
                ? m with { Text = SensitiveDataRedactor.Redact(m.Text, options).Text }
                : m)
            .ToList();

        var request = new AiCompletionRequest
        {
            Model = _settings.GeminiModel,
            ApiKey = key!,
            SystemInstruction = systemInstruction ?? AiPrompts.SupportSystemInstruction,
            Temperature = _settings.AiTemperature,
            MaxOutputTokens = 2048,
            Messages = redacted
        };

        var result = await _provider.CompleteAsync(request, ct).ConfigureAwait(false);

        // Self-heal: if the configured model is gone (Google retires them often),
        // discover a current one, persist it, and retry once.
        if (!result.Success && LooksLikeModelError(result.Error))
        {
            var models = await _provider.ListModelsAsync(key!, ct: ct).ConfigureAwait(false);
            var better = PickBestModel(models, request.Model);
            if (better is not null)
            {
                _settings.GeminiModel = better;
                LastAutoSelectedModel = better;
                result = await _provider.CompleteAsync(request with { Model = better }, ct).ConfigureAwait(false);
            }
        }

        return result;
    }

    /// <summary>Set when SendAsync auto-switched to a working model (for the UI to surface).</summary>
    public string? LastAutoSelectedModel { get; private set; }

    private static bool LooksLikeModelError(string? error)
    {
        if (string.IsNullOrEmpty(error)) return false;
        var e = error.ToLowerInvariant();
        return e.Contains("no longer available") || e.Contains("not found")
            || e.Contains("not supported") || e.Contains("is not found for api version")
            || e.Contains("update your code to use");
    }

    private static string? PickBestModel(IReadOnlyList<string> models, string current)
    {
        if (models.Count == 0) return null;
        bool Ok(string m) => !string.Equals(m, current, StringComparison.OrdinalIgnoreCase);
        // Prefer a non-preview "flash" model, then any flash, then any model.
        return models.FirstOrDefault(m => Ok(m) && m.Contains("flash", StringComparison.OrdinalIgnoreCase)
                                               && !m.Contains("thinking", StringComparison.OrdinalIgnoreCase)
                                               && !m.Contains("lite", StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(m => Ok(m) && m.Contains("flash", StringComparison.OrdinalIgnoreCase))
            ?? models.FirstOrDefault(Ok);
    }

    public Task<AiResult> SinglePromptAsync(string prompt, CancellationToken ct = default)
        => SendAsync(new[] { new AiMessage(AiRole.User, prompt) }, ct: ct);

    /// <summary>Lists models available to the configured key that support generateContent.</summary>
    public async Task<(bool Ok, IReadOnlyList<string> Models, string? Error)> ListModelsAsync(CancellationToken ct = default)
    {
        var key = _settings.GetGeminiApiKey();
        if (string.IsNullOrWhiteSpace(key))
            return (false, Array.Empty<string>(), "Save a Gemini API key first.");

        var models = await _provider.ListModelsAsync(key!, ct: ct).ConfigureAwait(false);
        return models.Count == 0
            ? (false, models, "No models returned — check the key and network.")
            : (true, models, null);
    }

    public Task<AiResult> DraftEmailAsync(
        EmailTemplate template,
        IReadOnlyDictionary<string, string> values,
        WritingTone tone,
        string? context,
        string? conversation = null,
        CancellationToken ct = default)
    {
        var prompt = AiPrompts.BuildEmailFromTemplate(template, values, tone, context, conversation);
        return SinglePromptAsync(prompt, ct);
    }
}
