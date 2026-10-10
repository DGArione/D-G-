using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Data;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class CalendarViewModel : ObservableObject
{
    private readonly CalendarRepository _repo;

    public CalendarViewModel(CalendarRepository repo)
    {
        _repo = repo;
        Events = new ObservableCollection<CalendarEvent>();
        Reload();
    }

    public ObservableCollection<CalendarEvent> Events { get; }
    public IReadOnlyList<string> Kinds { get; } = new[] { "Shift", "Event", "Meeting", "Reminder", "On-call" };

    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private DateTime? _newDate = DateTime.Today;
    [ObservableProperty] private string _newStart = "09:00";
    [ObservableProperty] private string _newEnd = "17:00";
    [ObservableProperty] private string _newKind = "Shift";
    [ObservableProperty] private string _status = string.Empty;

    public void Reload()
    {
        Events.Clear();
        foreach (var e in _repo.GetUpcoming()) Events.Add(e);
    }

    [RelayCommand]
    private void AddEvent()
    {
        if (string.IsNullOrWhiteSpace(NewTitle)) { Status = "Enter a title."; return; }
        var date = NewDate ?? DateTime.Today;
        if (!TimeSpan.TryParse(NewStart, out var s)) s = new TimeSpan(9, 0, 0);
        if (!TimeSpan.TryParse(NewEnd, out var e) || e <= s) e = s.Add(TimeSpan.FromHours(1));

        var startLocal = date.Date + s;
        var endLocal = date.Date + e;
        _repo.Add(new CalendarEvent(0, NewTitle.Trim(),
            new DateTimeOffset(startLocal).ToUniversalTime(),
            new DateTimeOffset(endLocal).ToUniversalTime(),
            NewKind, null, null));
        NewTitle = string.Empty;
        Reload();
        Status = "Added.";
    }

    [RelayCommand]
    private void DeleteEvent(CalendarEvent? e)
    {
        if (e is null) return;
        _repo.Delete(e.Id);
        Reload();
    }
}
