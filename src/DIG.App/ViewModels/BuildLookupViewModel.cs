using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Intelligence;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class BuildLookupViewModel : ObservableObject
{
    private readonly VmwareBuildLookupService _service;

    /// <summary>Raised to ask the shell to open a URL in the browser.</summary>
    public event Action<string>? NavigateRequested;

    public BuildLookupViewModel(VmwareBuildLookupService service)
    {
        _service = service;
        Recent = new ObservableCollection<BuildLookupResult>();
    }

    public ObservableCollection<BuildLookupResult> Recent { get; }

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private BuildLookupResult? _result;
    [ObservableProperty] private string _status = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LookupCommand))]
    private bool _isBusy;

    private bool CanLookup() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanLookup))]
    private async Task LookupAsync()
    {
        if (string.IsNullOrWhiteSpace(Query)) return;
        IsBusy = true;
        Status = "Looking up…";
        try
        {
            var result = await _service.LookupAsync(Query);
            Result = result;
            Status = result.Confidence switch
            {
                MatchConfidence.Verified => "Verified match.",
                MatchConfidence.Possible => "Possible match (bundled reference — verify on Broadcom).",
                _ => "No verified release match found."
            };
            Recent.Insert(0, result);
            while (Recent.Count > 15) Recent.RemoveAt(Recent.Count - 1);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SearchDocs()
    {
        var q = Result?.Build ?? Query;
        if (string.IsNullOrWhiteSpace(q)) return;
        var url = Result?.ReleaseNotesUrl
            ?? "https://www.google.com/search?q=" + Uri.EscapeDataString($"VMware build {q} release notes site:broadcom.com");
        NavigateRequested?.Invoke(url);
    }
}
