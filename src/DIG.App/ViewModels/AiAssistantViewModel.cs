using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.App.Services;
using DIG.Core.Ai;

namespace DIG.App.ViewModels;

public sealed class AiChatMessage
{
    public AiChatMessage(AiRole role, string text, bool isError = false)
    {
        Role = role;
        Text = text;
        IsError = isError;
    }

    public AiRole Role { get; }
    public string Text { get; }
    public bool IsError { get; }
    public bool IsUser => Role == AiRole.User && !IsError;
    public bool IsAssistant => Role == AiRole.Assistant && !IsError;

    public static AiChatMessage Err(string text) => new(AiRole.Assistant, text, true);
}

public sealed partial class AiAssistantViewModel : ObservableObject
{
    private readonly AiService _ai;

    public AiAssistantViewModel(AiService ai)
    {
        _ai = ai;
        Messages = new ObservableCollection<AiChatMessage>();
    }

    public ObservableCollection<AiChatMessage> Messages { get; }

    public string ProviderName => _ai.ProviderName;
    public bool IsConfigured => _ai.IsConfigured;
    public string ConfigurationHint => IsConfigured
        ? $"Connected to {ProviderName}."
        : $"{ProviderName} is not configured. Add your API key in Settings → AI.";

    [ObservableProperty]
    private string _input = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>Refreshes configuration-derived properties (call when settings change).</summary>
    public void RefreshConfiguration()
    {
        OnPropertyChanged(nameof(IsConfigured));
        OnPropertyChanged(nameof(ConfigurationHint));
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        var text = Input?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        Messages.Add(new AiChatMessage(AiRole.User, text));
        Input = string.Empty;
        IsBusy = true;
        Status = $"Contacting {_ai.ProviderName}…";

        try
        {
            var history = Messages
                .Where(m => !m.IsError)
                .Select(m => new AiMessage(m.Role, m.Text))
                .ToList();

            var result = await _ai.SendAsync(history);
            Messages.Add(result.Success
                ? new AiChatMessage(AiRole.Assistant, result.Text)
                : AiChatMessage.Err(result.Error ?? "Unknown error."));
        }
        catch (Exception ex)
        {
            Messages.Add(AiChatMessage.Err($"Request failed: {ex.Message}"));
        }
        finally
        {
            IsBusy = false;
            Status = string.Empty;
        }
    }

    private bool CanSend() => !IsBusy;

    [RelayCommand]
    private void Clear() => Messages.Clear();

    /// <summary>Seeds the input with a prompt (used by selection actions) and sends.</summary>
    public async Task AskAsync(string prompt)
    {
        Input = prompt;
        if (SendCommand.CanExecute(null))
            await SendCommand.ExecuteAsync(null);
    }
}
