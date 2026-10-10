using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Data;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class BookmarksViewModel : ObservableObject
{
    private readonly BookmarkRepository _repo;

    public event Action<string>? NavigateRequested;

    public BookmarksViewModel(BookmarkRepository repo)
    {
        _repo = repo;
        Bookmarks = new ObservableCollection<Bookmark>();
        Reload();
    }

    public ObservableCollection<Bookmark> Bookmarks { get; }

    public void Reload()
    {
        Bookmarks.Clear();
        foreach (var b in _repo.GetAll()) Bookmarks.Add(b);
    }

    [RelayCommand]
    private void Open(Bookmark? b)
    {
        if (b is not null) NavigateRequested?.Invoke(b.Url);
    }

    [RelayCommand]
    private void Delete(Bookmark? b)
    {
        if (b is not null)
        {
            _repo.Delete(b.Id);
            Reload();
        }
    }
}
