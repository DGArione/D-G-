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

        return await _provider.CompleteAsync(request, ct).ConfigureAwait(false);
    }

    public Task<AiResult> SinglePromptAsync(string prompt, CancellationToken ct = default)
        => SendAsync(new[] { new AiMessage(AiRole.User, prompt) }, ct: ct);

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
