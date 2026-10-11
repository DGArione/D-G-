using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Data;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class CalendarDay : ObservableObject
{
    public CalendarDay(DateTime date, bool inMonth, bool isToday, IReadOnlyList<CalendarEvent> events)
    {
        Date = date;
        InMonth = inMonth;
        IsToday = isToday;
        Events = events;
    }

    public DateTime Date { get; }
    public bool InMonth { get; }
    public bool IsToday { get; }
    public IReadOnlyList<CalendarEvent> Events { get; }
    public int Day => Date.Day;
    public bool HasEvents => Events.Count > 0;

    [ObservableProperty] private bool _isSelected;
}

public sealed partial class CalendarViewModel : ObservableObject
{
    private readonly CalendarRepository _repo;

    public CalendarViewModel(CalendarRepository repo)
    {
        _repo = repo;
        Days = new ObservableCollection<CalendarDay>();
        SelectedDayEvents = new ObservableCollection<CalendarEvent>();
        _displayedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _newDate = DateTime.Today;
        BuildMonth();
        SelectDate(DateTime.Today);
    }

    public ObservableCollection<CalendarDay> Days { get; }
    public ObservableCollection<CalendarEvent> SelectedDayEvents { get; }
    public IReadOnlyList<string> WeekdayHeaders { get; } = new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    public IReadOnlyList<string> Kinds { get; } = new[] { "Shift", "Event", "Meeting", "Reminder", "On-call" };

    [ObservableProperty] private DateTime _displayedMonth;
    [ObservableProperty] private string _monthLabel = string.Empty;
    [ObservableProperty] private string _selectedDayLabel = string.Empty;

    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private DateTime? _newDate;
    [ObservableProperty] private string _newStart = "09:00";
    [ObservableProperty] private string _newEnd = "17:00";
    [ObservableProperty] private string _newKind = "Shift";
    [ObservableProperty] private string _status = string.Empty;

    public void Reload() { BuildMonth(); SelectDate(NewDate ?? DateTime.Today); }

    private void BuildMonth()
    {
        var first = new DateTime(DisplayedMonth.Year, DisplayedMonth.Month, 1);
        var lead = (int)first.DayOfWeek; // Sunday = 0
        var gridStart = first.AddDays(-lead);
        var today = DateTime.Today;
        var evs = _repo.GetUpcoming(1000);

        Days.Clear();
        for (var i = 0; i < 42; i++)
        {
            var d = gridStart.AddDays(i);
            var dayEvents = evs.Where(e => e.StartUtc.ToLocalTime().Date == d.Date)
                               .OrderBy(e => e.StartUtc).ToList();
            Days.Add(new CalendarDay(d, d.Month == DisplayedMonth.Month, d.Date == today, dayEvents));
        }
        MonthLabel = DisplayedMonth.ToString("MMMM yyyy");
    }

    private void SelectDate(DateTime date)
    {
        NewDate = date.Date;
        SelectedDayLabel = date.ToString("dddd, dd MMMM yyyy");
        foreach (var d in Days) d.IsSelected = d.Date.Date == date.Date;
        SelectedDayEvents.Clear();
        var day = Days.FirstOrDefault(d => d.Date.Date == date.Date);
        if (day is not null)
            foreach (var e in day.Events) SelectedDayEvents.Add(e);
    }

    [RelayCommand] private void PrevMonth() { DisplayedMonth = DisplayedMonth.AddMonths(-1); BuildMonth(); }
    [RelayCommand] private void NextMonth() { DisplayedMonth = DisplayedMonth.AddMonths(1); BuildMonth(); }

    [RelayCommand]
    private void Today()
    {
        DisplayedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        BuildMonth();
        SelectDate(DateTime.Today);
    }

    [RelayCommand]
    private void SelectDay(CalendarDay? day)
    {
        if (day is null) return;
        if (day.Date.Month != DisplayedMonth.Month) { DisplayedMonth = new DateTime(day.Date.Year, day.Date.Month, 1); BuildMonth(); }
        SelectDate(day.Date);
    }

    [RelayCommand]
    private void AddEvent()
    {
        if (string.IsNullOrWhiteSpace(NewTitle)) { Status = "Enter a title."; return; }
        var date = (NewDate ?? DateTime.Today).Date;
        if (!TimeSpan.TryParse(NewStart, out var s)) s = new TimeSpan(9, 0, 0);
        if (!TimeSpan.TryParse(NewEnd, out var e) || e <= s) e = s.Add(TimeSpan.FromHours(1));

        _repo.Add(new CalendarEvent(0, NewTitle.Trim(),
            new DateTimeOffset(date + s).ToUniversalTime(),
            new DateTimeOffset(date + e).ToUniversalTime(),
            NewKind, null, null));
        NewTitle = string.Empty;
        BuildMonth();
        SelectDate(date);
        Status = "Added.";
    }

    [RelayCommand]
    private void DeleteEvent(CalendarEvent? e)
    {
        if (e is null) return;
        _repo.Delete(e.Id);
        BuildMonth();
        SelectDate(NewDate ?? DateTime.Today);
    }
}
