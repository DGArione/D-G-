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
}

/// <summary>Known Gemini model identifiers offered in settings (user may type others).</summary>
public static class GeminiModels
{
    public const string Flash = "gemini-1.5-flash";
    public const string FlashLatest = "gemini-1.5-flash-latest";
    public const string Pro = "gemini-1.5-pro";
    public const string ProLatest = "gemini-1.5-pro-latest";

    public static readonly IReadOnlyList<string> Suggested = new[]
    {
        Flash, FlashLatest, Pro, ProLatest
    };

    public const string Default = Flash;
}
