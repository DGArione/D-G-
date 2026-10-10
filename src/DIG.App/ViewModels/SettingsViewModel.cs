using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.App.Services;
using DIG.Core.Ai;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsService _settings;
    private readonly AiService _ai;

    /// <summary>Raised when a setting that other view-models care about changes.</summary>
    public event Action? SettingsChanged;

    public SettingsViewModel(AppSettingsService settings, AiService ai)
    {
        _settings = settings;
        _ai = ai;

        // Initialise backing fields directly so loading does not re-persist / re-apply.
        _selectedThemePreset = ThemePresets.ById(settings.ThemePresetId);
        _customBackgroundPath = settings.CustomBackgroundPath;
        _searchEngine = settings.SearchEngine;
        _homepage = settings.Homepage;
        _geminiModel = settings.GeminiModel;
        _aiTemperature = settings.AiTemperature;
        _redactNetworkBeforeAi = settings.RedactNetworkBeforeAi;
        _aiEnabled = settings.AiEnabled;

        UpdateApiKeyStatus();
    }

    public IReadOnlyList<ThemePreset> ThemePresetsList { get; } = ThemePresets.All;
    public IReadOnlyList<SearchEngine> SearchEngines { get; } = Enum.GetValues<SearchEngine>();
    public System.Collections.ObjectModel.ObservableCollection<string> AvailableModels { get; }
        = new(GeminiModels.Suggested);

    public string SecretBackendDescription => _settings.SecretBackendDescription;

    [ObservableProperty] private ThemePreset _selectedThemePreset;
    [ObservableProperty] private string? _customBackgroundPath;
    [ObservableProperty] private SearchEngine _searchEngine;
    [ObservableProperty] private string _homepage;
    [ObservableProperty] private string _geminiModel;
    [ObservableProperty] private double _aiTemperature;
    [ObservableProperty] private bool _redactNetworkBeforeAi;
    [ObservableProperty] private bool _aiEnabled;

    [ObservableProperty] private string _geminiApiKeyInput = string.Empty;
    [ObservableProperty] private bool _hasGeminiApiKey;
    [ObservableProperty] private string _apiKeyStatus = string.Empty;
    [ObservableProperty] private string _testResult = string.Empty;

    partial void OnSelectedThemePresetChanged(ThemePreset value)
    {
        if (value is null) return;
        _settings.ThemePresetId = value.Id;
        ThemeManager.Apply(value, _settings.CustomBackgroundPath);
    }

    [RelayCommand]
    private void BrowseBackground()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose a background image",
            Filter = "Images (*.jpg;*.jpeg;*.png;*.bmp;*.webp)|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        CustomBackgroundPath = dlg.FileName;
        _settings.CustomBackgroundPath = dlg.FileName;
        ThemeManager.Apply(SelectedThemePreset, dlg.FileName);
    }

    [RelayCommand]
    private void ClearBackground()
    {
        CustomBackgroundPath = null;
        _settings.CustomBackgroundPath = null;
        ThemeManager.Apply(SelectedThemePreset, null);
    }

    partial void OnSearchEngineChanged(SearchEngine value) { _settings.SearchEngine = value; SettingsChanged?.Invoke(); }
    partial void OnHomepageChanged(string value) => _settings.Homepage = value;
    partial void OnGeminiModelChanged(string value) => _settings.GeminiModel = value;
    partial void OnAiTemperatureChanged(double value) => _settings.AiTemperature = value;
    partial void OnRedactNetworkBeforeAiChanged(bool value) => _settings.RedactNetworkBeforeAi = value;

    partial void OnAiEnabledChanged(bool value)
    {
        _settings.AiEnabled = value;
        SettingsChanged?.Invoke();
    }

    [RelayCommand]
    private void SaveApiKey()
    {
        var key = GeminiApiKeyInput?.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            ApiKeyStatus = "Enter a key first.";
            return;
        }
        _settings.SetGeminiApiKey(key);
        GeminiApiKeyInput = string.Empty; // never keep the raw key in a bound field
        TestResult = string.Empty;
        UpdateApiKeyStatus();
        SettingsChanged?.Invoke();
    }

    [RelayCommand]
    private void ClearApiKey()
    {
        _settings.SetGeminiApiKey(null);
        GeminiApiKeyInput = string.Empty;
        TestResult = string.Empty;
        UpdateApiKeyStatus();
        SettingsChanged?.Invoke();
    }

    [RelayCommand]
    private async Task ListModelsAsync()
    {
        if (!_settings.HasGeminiApiKey)
        {
            TestResult = "Save a key before listing models.";
            return;
        }
        TestResult = "Fetching available models…";
        var (ok, models, error) = await _ai.ListModelsAsync();
        if (!ok)
        {
            TestResult = error ?? "Could not list models.";
            return;
        }
        AvailableModels.Clear();
        foreach (var m in models) AvailableModels.Add(m);
        if (!AvailableModels.Contains(GeminiModel) && AvailableModels.Count > 0)
            GeminiModel = AvailableModels.FirstOrDefault(m => m.Contains("flash", StringComparison.OrdinalIgnoreCase)) ?? AvailableModels[0];
        TestResult = $"Found {AvailableModels.Count} model(s). Pick one above.";
    }

    [RelayCommand]
    private async Task TestApiKeyAsync()
    {
        if (!_settings.HasGeminiApiKey)
        {
            TestResult = "Save a key before testing.";
            return;
        }
        TestResult = "Testing…";
        var result = await _ai.SinglePromptAsync("Reply with exactly: OK");
        TestResult = result.Success
            ? $"Success — model responded ({_settings.GeminiModel})."
            : $"Failed: {result.Error}";
    }

    private void UpdateApiKeyStatus()
    {
        HasGeminiApiKey = _settings.HasGeminiApiKey;
        ApiKeyStatus = HasGeminiApiKey
            ? $"A key is stored, encrypted via {_settings.SecretBackendDescription}."
            : "No API key stored.";
    }
}
