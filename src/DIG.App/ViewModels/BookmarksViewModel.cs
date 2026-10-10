using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Data;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed record BookmarkGroup(string Folder, IReadOnlyList<Bookmark> Items);

public sealed partial class BookmarksViewModel : ObservableObject
{
    private readonly BookmarkRepository _repo;

    public event Action<string>? NavigateRequested;

    public BookmarksViewModel(BookmarkRepository repo)
    {
        _repo = repo;
        Groups = new ObservableCollection<BookmarkGroup>();
        Reload();
    }

    /// <summary>Bookmarks grouped by folder/category.</summary>
    public ObservableCollection<BookmarkGroup> Groups { get; }

    [ObservableProperty] private Bookmark? _selected;
    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editFolder = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    partial void OnSelectedChanged(Bookmark? value)
    {
        if (value is null) return;
        EditTitle = value.Title;
        EditFolder = value.Folder ?? string.Empty;
    }

    public void Reload()
    {
        Groups.Clear();
        var grouped = _repo.GetAll()
            .GroupBy(b => string.IsNullOrWhiteSpace(b.Folder) ? "Unsorted" : b.Folder!)
            .OrderBy(g => g.Key == "Unsorted" ? "~~~" : g.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var g in grouped)
            Groups.Add(new BookmarkGroup(g.Key, g.ToList()));
    }

    [RelayCommand]
    private void Open(Bookmark? b)
    {
        if (b is not null) NavigateRequested?.Invoke(b.Url);
    }

    [RelayCommand]
    private void Select(Bookmark? b)
    {
        if (b is not null) Selected = b;
    }

    [RelayCommand]
    private void Delete(Bookmark? b)
    {
        var target = b ?? Selected;
        if (target is null) return;
        _repo.Delete(target.Id);
        Reload();
        Status = "Bookmark deleted.";
    }

    [RelayCommand]
    private void Save()
    {
        if (Selected is null) { Status = "Select a bookmark first."; return; }
        _repo.Update(Selected.Id, string.IsNullOrWhiteSpace(EditTitle) ? Selected.Url : EditTitle.Trim(),
            string.IsNullOrWhiteSpace(EditFolder) ? null : EditFolder.Trim());
        Reload();
        Status = "Saved.";
    }
}
