using DIG.Core.Ai;
using DIG.Core.Data;
using DIG.Core.Models;
using DIG.Core.Security;

namespace DIG.App.Services;

public enum AppTheme { Dark, Light, System }

/// <summary>
/// Typed access to application settings. Non-secret values live in the SQLite
/// settings table; the Gemini API key lives ONLY in the encrypted secret store.
/// </summary>
public sealed class AppSettingsService
{
    public const string GeminiKeyName = "gemini_api_key";

    private readonly SettingsRepository _settings;
    private readonly ISecretStore _secrets;

    public AppSettingsService(SettingsRepository settings, ISecretStore secrets)
    {
        _settings = settings;
        _secrets = secrets;
    }

    public string SecretBackendDescription => _secrets.BackendDescription;

    public AppTheme Theme
    {
        get => Enum.TryParse<AppTheme>(_settings.Get("theme", nameof(AppTheme.Dark)), out var t) ? t : AppTheme.Dark;
        set => _settings.Set("theme", value.ToString());
    }

    public string ThemePresetId
    {
        get => _settings.Get("theme_preset", "obsidian");
        set => _settings.Set("theme_preset", value);
    }

    public string? CustomBackgroundPath
    {
        get { var v = _settings.Get("bg_path", ""); return string.IsNullOrWhiteSpace(v) ? null : v; }
        set => _settings.Set("bg_path", value ?? "");
    }

    public SearchEngine SearchEngine
    {
        get => Enum.TryParse<SearchEngine>(_settings.Get("search_engine", nameof(SearchEngine.Google)), out var e) ? e : SearchEngine.Google;
        set => _settings.Set("search_engine", value.ToString());
    }

    public string Homepage
    {
        get => _settings.Get("homepage", "https://www.google.com");
        set => _settings.Set("homepage", value);
    }

    public string GeminiModel
    {
        get
        {
            var stored = _settings.Get("gemini_model", GeminiModels.Default);
            // Migrate off retired models (e.g. gemini-1.5-*) so the AI keeps working.
            if (GeminiModels.IsDeprecated(stored))
            {
                _settings.Set("gemini_model", GeminiModels.Default);
                return GeminiModels.Default;
            }
            return stored;
        }
        set => _settings.Set("gemini_model", value);
    }

    public double AiTemperature
    {
        get => double.TryParse(_settings.Get("ai_temperature", "0.3"), out var v) ? v : 0.3;
        set => _settings.Set("ai_temperature", value.ToString("0.0#"));
    }

    public bool RedactNetworkBeforeAi
    {
        get => _settings.Get("ai_redact_network", "false") == "true";
        set => _settings.Set("ai_redact_network", value ? "true" : "false");
    }

    public bool AiEnabled
    {
        get => _settings.Get("ai_enabled", "true") == "true";
        set => _settings.Set("ai_enabled", value ? "true" : "false");
    }

    // ---- Secret: Gemini API key ----

    public bool HasGeminiApiKey => _secrets.Exists(GeminiKeyName)
        && !string.IsNullOrWhiteSpace(_secrets.Get(GeminiKeyName));

    public string? GetGeminiApiKey() => _secrets.Get(GeminiKeyName);

    public void SetGeminiApiKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            _secrets.Delete(GeminiKeyName);
        else
            _secrets.Set(GeminiKeyName, key.Trim());
    }
}
