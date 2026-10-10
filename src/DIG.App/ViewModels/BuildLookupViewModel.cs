using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Data;
using DIG.Core.Intelligence;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class BuildLookupViewModel : ObservableObject
{
    private readonly VmwareBuildLookupService _service;
    private readonly BuildCacheRepository _cache;

    /// <summary>Raised to ask the shell to open a URL in the browser.</summary>
    public event Action<string>? NavigateRequested;

    /// <summary>Raised to ask the shell to scrape build tables from the active page.</summary>
    public event Action? ImportFromPageRequested;

    public BuildLookupViewModel(VmwareBuildLookupService service, BuildCacheRepository cache)
    {
        _service = service;
        _cache = cache;
        Recent = new ObservableCollection<BuildLookupResult>();
        UpdateCounts();
    }

    public ObservableCollection<BuildLookupResult> Recent { get; }

    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private BuildLookupResult? _result;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _sourceInfo = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LookupCommand))]
    private bool _isBusy;

    private void UpdateCounts()
    {
        var imported = _cache.Count();
        SourceInfo = imported > 0
            ? $"{imported} imported build(s) cached, plus the bundled reference index."
            : "Using the bundled reference index. Import the full list from a Broadcom page or a CSV.";
    }

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
                MatchConfidence.Possible => "Possible match — verify on Broadcom.",
                _ => "No match found. Try importing the full list (below)."
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

    /// <summary>Open the Broadcom "build numbers and versions" KB pages in the browser.</summary>
    [RelayCommand]
    private void OpenBroadcomKb()
        => NavigateRequested?.Invoke("https://knowledge.broadcom.com/external/article/316595/build-numbers-and-versions-of-vmware-esx.html");

    /// <summary>Scrape build/version tables from the page currently open in the browser.</summary>
    [RelayCommand]
    private void ImportFromPage()
    {
        Status = "Reading the current browser page…";
        ImportFromPageRequested?.Invoke();
    }

    /// <summary>Called by the shell after a page import completes.</summary>
    public void OnPageImported(int count, string? error)
    {
        if (error is not null) { Status = error; return; }
        Status = count > 0
            ? $"Imported {count} build(s) from the page. They now show as Verified in lookups."
            : "No build tables found on the current page. Open a Broadcom 'build numbers and versions' KB page first.";
        UpdateCounts();
    }

    [RelayCommand]
    private void ImportCsv()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import VMware builds from CSV",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var text = File.ReadAllText(dlg.FileName);
            var records = BuildCsvImporter.Parse(text);
            var n = _cache.AddRange(records, "csv", "Possible", dlg.FileName);
            Status = n > 0 ? $"Imported {n} build(s) from CSV." : "No valid rows found in the CSV.";
            UpdateCounts();
        }
        catch (Exception ex)
        {
            Status = $"CSV import failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ClearImported()
    {
        _cache.Clear("csv");
        _cache.Clear("browser-import");
        Status = "Cleared imported builds. Bundled reference index still active.";
        UpdateCounts();
    }
}
