using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Data;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class TasksViewModel : ObservableObject
{
    private readonly TaskRepository _tasks;
    private readonly NoteRepository _notes;

    public TasksViewModel(TaskRepository tasks, NoteRepository notes)
    {
        _tasks = tasks;
        _notes = notes;
        Tasks = new ObservableCollection<TaskItem>();
        Notes = new ObservableCollection<NoteItem>();
        Reload();
    }

    public ObservableCollection<TaskItem> Tasks { get; }
    public ObservableCollection<NoteItem> Notes { get; }
    public IReadOnlyList<string> Priorities { get; } = new[] { "Low", "Normal", "High", "Urgent" };

    [ObservableProperty] private string _newTaskTitle = string.Empty;
    [ObservableProperty] private string _newTaskPriority = "Normal";
    [ObservableProperty] private string _status = string.Empty;

    [ObservableProperty] private NoteItem? _selectedNote;
    [ObservableProperty] private string _editNoteTitle = string.Empty;
    [ObservableProperty] private string _editNoteBody = string.Empty;

    partial void OnSelectedNoteChanged(NoteItem? value)
    {
        if (value is null) return;
        EditNoteTitle = value.Title;
        EditNoteBody = value.Body;
    }

    public void Reload()
    {
        Tasks.Clear();
        foreach (var t in _tasks.GetAll()) Tasks.Add(t);
        Notes.Clear();
        foreach (var n in _notes.GetAll()) Notes.Add(n);
    }

    [RelayCommand]
    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;
        _tasks.Upsert(new TaskItem(0, NewTaskTitle.Trim(), false, NewTaskPriority, null, null, DateTimeOffset.UtcNow));
        NewTaskTitle = string.Empty;
        Reload();
    }

    [RelayCommand]
    private void ToggleDone(TaskItem? t)
    {
        if (t is null) return;
        _tasks.SetDone(t.Id, !t.Done);
        Reload();
    }

    [RelayCommand]
    private void DeleteTask(TaskItem? t)
    {
        if (t is null) return;
        _tasks.Delete(t.Id);
        Reload();
    }

    [RelayCommand]
    private void NewNote()
    {
        SelectedNote = null;
        EditNoteTitle = "New note";
        EditNoteBody = string.Empty;
    }

    [RelayCommand]
    private void SaveNote()
    {
        var id = _notes.Upsert(new NoteItem(SelectedNote?.Id ?? 0,
            string.IsNullOrWhiteSpace(EditNoteTitle) ? "Untitled" : EditNoteTitle.Trim(),
            EditNoteBody, null, DateTimeOffset.UtcNow));
        Reload();
        SelectedNote = Notes.FirstOrDefault(n => n.Id == id);
        Status = "Note saved.";
    }

    [RelayCommand]
    private void DeleteNote(NoteItem? n)
    {
        var target = n ?? SelectedNote;
        if (target is null) return;
        _notes.Delete(target.Id);
        SelectedNote = null;
        Reload();
    }
}
