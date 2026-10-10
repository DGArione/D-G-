using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Data;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly HistoryRepository _repo;

    public event Action<string>? NavigateRequested;

    public HistoryViewModel(HistoryRepository repo)
    {
        _repo = repo;
        Entries = new ObservableCollection<HistoryEntry>();
        Reload();
    }

    public ObservableCollection<HistoryEntry> Entries { get; }

    public void Reload()
    {
        Entries.Clear();
        foreach (var e in _repo.GetRecent()) Entries.Add(e);
    }

    [RelayCommand]
    private void Open(HistoryEntry? e)
    {
        if (e is not null) NavigateRequested?.Invoke(e.Url);
    }

    [RelayCommand]
    private void ClearAll()
    {
        _repo.Clear();
        Reload();
    }
}
