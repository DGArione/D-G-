using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Data;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class KnowledgeBaseViewModel : ObservableObject
{
    private readonly KbRepository _repo;

    public KnowledgeBaseViewModel(KbRepository repo)
    {
        _repo = repo;
        Entries = new ObservableCollection<KbEntry>();
        Reload();
    }

    public ObservableCollection<KbEntry> Entries { get; }

    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private KbEntry? _selected;
    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editContent = string.Empty;
    [ObservableProperty] private string _editSource = string.Empty;
    [ObservableProperty] private string _editTags = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    partial void OnSearchTermChanged(string value) => Reload();

    partial void OnSelectedChanged(KbEntry? value)
    {
        if (value is null) return;
        EditTitle = value.Title;
        EditContent = value.Content;
        EditSource = value.Source ?? "";
        EditTags = value.Tags ?? "";
    }

    public void Reload()
    {
        Entries.Clear();
        foreach (var e in _repo.Search(SearchTerm)) Entries.Add(e);
    }

    [RelayCommand]
    private void NewEntry()
    {
        Selected = null;
        EditTitle = "New article"; EditContent = ""; EditSource = ""; EditTags = "";
    }

    [RelayCommand]
    private void Save()
    {
        var id = _repo.Upsert(new KbEntry(Selected?.Id ?? 0,
            string.IsNullOrWhiteSpace(EditTitle) ? "Untitled" : EditTitle.Trim(),
            EditContent,
            string.IsNullOrWhiteSpace(EditSource) ? null : EditSource.Trim(),
            string.IsNullOrWhiteSpace(EditTags) ? null : EditTags.Trim(),
            DateTimeOffset.UtcNow));
        Reload();
        Selected = Entries.FirstOrDefault(e => e.Id == id);
        Status = "Saved.";
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is { Id: > 0 } e) { _repo.Delete(e.Id); Selected = null; Reload(); Status = "Deleted."; }
    }
}
