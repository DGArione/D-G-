using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Data;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class WorkspacesViewModel : ObservableObject
{
    private readonly WorkspaceRepository _repo;

    /// <summary>Raised to save the current browser tabs under this name (shell supplies the URLs).</summary>
    public event Action<string>? SaveCurrentRequested;

    /// <summary>Raised to open all of a workspace's tabs.</summary>
    public event Action<Workspace>? OpenRequested;

    public WorkspacesViewModel(WorkspaceRepository repo)
    {
        _repo = repo;
        Workspaces = new ObservableCollection<Workspace>();
        Reload();
    }

    public ObservableCollection<Workspace> Workspaces { get; }

    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private Workspace? _selected;
    [ObservableProperty] private string _status = string.Empty;

    public void Reload()
    {
        Workspaces.Clear();
        foreach (var w in _repo.GetAll()) Workspaces.Add(w);
    }

    [RelayCommand]
    private void SaveCurrent()
    {
        var name = string.IsNullOrWhiteSpace(NewName) ? $"Workspace {DateTime.Now:yyyy-MM-dd HH:mm}" : NewName.Trim();
        SaveCurrentRequested?.Invoke(name);
        NewName = string.Empty;
    }

    /// <summary>Called by the shell to persist the captured URLs.</summary>
    public void CommitSaved(string name, IReadOnlyList<string> urls)
    {
        if (urls.Count == 0) { Status = "No open tabs to save."; return; }
        _repo.Upsert(0, name, urls);
        Reload();
        Status = $"Saved '{name}' with {urls.Count} tab(s).";
    }

    [RelayCommand]
    private void Open(Workspace? w)
    {
        var ws = w ?? Selected;
        if (ws is null || ws.Urls.Count == 0) { Status = "This workspace has no tabs."; return; }
        OpenRequested?.Invoke(ws);
        Status = $"Opening '{ws.Name}'…";
    }

    [RelayCommand]
    private void Delete(Workspace? w)
    {
        var ws = w ?? Selected;
        if (ws is null) return;
        _repo.Delete(ws.Id);
        Reload();
        Status = "Workspace deleted.";
    }
}
