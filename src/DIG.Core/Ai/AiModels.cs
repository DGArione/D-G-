namespace DIG.Core.Ai;

public enum AiRole
{
    User,
    Assistant
}

public sealed record AiMessage(AiRole Role, string Text);

/// <summary>A provider-neutral completion request.</summary>
public sealed record AiCompletionRequest
{
    public required string Model { get; init; }
    public required string ApiKey { get; init; }
    public required IReadOnlyList<AiMessage> Messages { get; init; }
    public string? SystemInstruction { get; init; }
    public double? Temperature { get; init; }
    public int? MaxOutputTokens { get; init; }

    /// <summary>Optional override of the provider base URL (used by tests).</summary>
    public string? BaseUrlOverride { get; init; }
}

public sealed record AiResult(bool Success, string Text, string? Error = null, string? Model = null)
{
    public static AiResult Ok(string text, string? model = null) => new(true, text, null, model);
    public static AiResult Fail(string error) => new(false, string.Empty, error);
}

/// <summary>Provider-neutral AI completion interface. Keys are passed per request
/// (resolved from the secret store) so providers stay stateless and testable.</summary>
public interface IAiProvider
{
    string Id { get; }
    string DisplayName { get; }
    Task<AiResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default);

    /// <summary>Lists model ids that support text generation for the given key.</summary>
    Task<IReadOnlyList<string>> ListModelsAsync(string apiKey, string? baseUrlOverride = null, CancellationToken ct = default);
}

/// <summary>Known Gemini model identifiers offered in settings (user may type others,
/// or pull the live list with "List models"). Defaults to a current model because the
/// 1.5 family is being retired on the Generative Language API.</summary>
public static class GeminiModels
{
    public const string Flash20 = "gemini-2.0-flash";
    public const string Flash25 = "gemini-2.5-flash";
    public const string Pro25 = "gemini-2.5-pro";
    public const string FlashLatest = "gemini-flash-latest";
    public const string ProLatest = "gemini-pro-latest";

    public static readonly IReadOnlyList<string> Suggested = new[]
    {
        Flash20, Flash25, Pro25, FlashLatest, ProLatest
    };

    public const string Default = Flash20;

    /// <summary>True for models that are retired / not served on v1beta generateContent,
    /// so a stored value can be migrated to <see cref="Default"/>.</summary>
    public static bool IsDeprecated(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return true;
        var m = model.Trim();
        return m.Contains("1.5", StringComparison.Ordinal)
            || m.Contains("1.0", StringComparison.Ordinal)
            || m.Equals("gemini-pro", StringComparison.OrdinalIgnoreCase)
            || m.Equals("gemini-pro-vision", StringComparison.OrdinalIgnoreCase);
    }
}
