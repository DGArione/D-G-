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
    public const string FlashLatest = "gemini-flash-latest";
    public const string ProLatest = "gemini-pro-latest";

    public static readonly IReadOnlyList<string> Suggested = new[]
    {
        FlashLatest, ProLatest, "gemini-2.5-flash", "gemini-2.5-pro", "gemini-2.0-flash"
    };

    // Alias that tracks the current flash model, so it survives model retirements;
    // if it ever fails, AiService auto-discovers a working model.
    public const string Default = FlashLatest;

    /// <summary>True for models known to be retired / not served, so a stored value
    /// can be migrated. (Runtime also self-heals on a model error.)</summary>
    public static bool IsDeprecated(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return true;
        var m = model.Trim();
        return m.Contains("1.5", StringComparison.Ordinal)
            || m.Contains("1.0", StringComparison.Ordinal)
            || m.Contains("2.0", StringComparison.Ordinal)
            || m.Equals("gemini-pro", StringComparison.OrdinalIgnoreCase)
            || m.Equals("gemini-pro-vision", StringComparison.OrdinalIgnoreCase);
    }
}
