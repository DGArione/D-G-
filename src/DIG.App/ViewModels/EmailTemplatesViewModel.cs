using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.App.Services;
using DIG.Core.Ai;
using DIG.Core.Data;
using DIG.Core.Email;

namespace DIG.App.ViewModels;

/// <summary>A single {{variable}} with its current value for the selected template.</summary>
public sealed partial class VariableInput : ObservableObject
{
    public VariableInput(string name, string value = "")
    {
        Name = name;
        _value = value;
    }

    public string Name { get; }

    [ObservableProperty]
    private string _value;
}

public sealed partial class EmailTemplatesViewModel : ObservableObject
{
    private readonly EmailTemplateRepository _repo;
    private readonly AiService _ai;

    public EmailTemplatesViewModel(EmailTemplateRepository repo, AiService ai)
    {
        _repo = repo;
        _ai = ai;
        Templates = new ObservableCollection<EmailTemplate>();
        Variables = new ObservableCollection<VariableInput>();
        Reload();
        if (Templates.Count > 0) SelectedTemplate = Templates[0];
    }

    public ObservableCollection<EmailTemplate> Templates { get; }
    public ObservableCollection<VariableInput> Variables { get; }
    public IReadOnlyList<WritingTone> Tones { get; } = Enum.GetValues<WritingTone>();

    public bool IsAiConfigured => _ai.IsConfigured;

    [ObservableProperty] private EmailTemplate? _selectedTemplate;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editCategory = "Response";
    [ObservableProperty] private string _editSubject = string.Empty;
    [ObservableProperty] private string _editBody = string.Empty;
    [ObservableProperty] private WritingTone _selectedTone = WritingTone.Professional;
    [ObservableProperty] private string _extraContext = string.Empty;
    [ObservableProperty] private string _conversation = string.Empty;
    [ObservableProperty] private string _draftOutput = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateWithAiCommand))]
    private bool _isBusy;

    partial void OnSelectedTemplateChanged(EmailTemplate? value)
    {
        if (value is null) return;
        EditName = value.Name;
        EditCategory = value.Category;
        EditSubject = value.Subject;
        EditBody = value.Body;
        RebuildVariables();
    }

    private void Reload()
    {
        _repo.SeedBuiltInsIfEmpty();
        Templates.Clear();
        foreach (var t in _repo.GetAll()) Templates.Add(t);
    }

    [RelayCommand]
    private void RebuildVariables()
    {
        var existing = Variables.ToDictionary(v => v.Name, v => v.Value, StringComparer.OrdinalIgnoreCase);
        var names = EmailTemplateRenderer.ExtractVariables(EditSubject + "\n" + EditBody);
        Variables.Clear();
        foreach (var n in names)
            Variables.Add(new VariableInput(n, existing.TryGetValue(n, out var val) ? val : string.Empty));
    }

    private EmailTemplate CurrentEdited() => new(
        SelectedTemplate?.Id ?? 0,
        string.IsNullOrWhiteSpace(EditName) ? "Untitled" : EditName,
        string.IsNullOrWhiteSpace(EditCategory) ? "General" : EditCategory,
        EditSubject, EditBody,
        SelectedTemplate?.IsBuiltIn ?? false,
        DateTimeOffset.UtcNow);

    private IReadOnlyDictionary<string, string> Values()
        => Variables.ToDictionary(v => v.Name, v => v.Value, StringComparer.OrdinalIgnoreCase);

    [RelayCommand]
    private void NewTemplate()
    {
        SelectedTemplate = null;
        EditName = "New template";
        EditCategory = "Response";
        EditSubject = "[{{case_number}}] ";
        EditBody = "Hello {{customer}},\n\n\n\nBest regards,\n{{engineer}}";
        RebuildVariables();
        DraftOutput = string.Empty;
    }

    [RelayCommand]
    private void SaveTemplate()
    {
        var edited = CurrentEdited();
        var id = _repo.Upsert(edited);
        Reload();
        SelectedTemplate = Templates.FirstOrDefault(t => t.Id == id);
        Status = "Template saved.";
    }

    [RelayCommand]
    private void DeleteTemplate()
    {
        if (SelectedTemplate is { Id: > 0 } t)
        {
            _repo.Delete(t.Id);
            Reload();
            SelectedTemplate = Templates.FirstOrDefault();
            Status = "Template deleted.";
        }
    }

    /// <summary>Fills the template using the entered values only — no AI, works offline.</summary>
    [RelayCommand]
    private void FillLocally()
    {
        var (subject, body) = EmailTemplateRenderer.Render(CurrentEdited(), Values());
        DraftOutput = $"Subject: {subject}\n\n{body}";
        Status = "Filled from template (offline, no AI).";
    }

    private bool CanGenerate() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateWithAiAsync()
    {
        if (!_ai.IsConfigured)
        {
            Status = "AI is not configured. Add a Gemini API key in Settings → AI, or use \"Fill from template\".";
            return;
        }

        IsBusy = true;
        Status = $"Drafting with {_ai.ProviderName}…";
        try
        {
            var result = await _ai.DraftEmailAsync(CurrentEdited(), Values(), SelectedTone,
                string.IsNullOrWhiteSpace(ExtraContext) ? null : ExtraContext,
                string.IsNullOrWhiteSpace(Conversation) ? null : Conversation);
            if (result.Success)
            {
                DraftOutput = result.Text;
                Status = "Draft ready for your review. Nothing is sent automatically.";
            }
            else
            {
                Status = $"Failed: {result.Error}";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CopyDraft()
    {
        if (string.IsNullOrEmpty(DraftOutput)) return;
        try
        {
            System.Windows.Clipboard.SetText(DraftOutput);
            Status = "Draft copied to clipboard.";
        }
        catch
        {
            Status = "Could not access the clipboard.";
        }
    }

    public void RefreshConfiguration() => OnPropertyChanged(nameof(IsAiConfigured));
}
