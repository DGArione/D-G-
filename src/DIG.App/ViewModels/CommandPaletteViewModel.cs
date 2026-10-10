using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DIG.App.ViewModels;

public sealed record PaletteCommand(string Name, string Hint, Action Run);

/// <summary>Backs the Ctrl+K global command palette.</summary>
public sealed partial class CommandPaletteViewModel : ObservableObject
{
    private readonly List<PaletteCommand> _all = new();

    public CommandPaletteViewModel()
    {
        Results = new ObservableCollection<PaletteCommand>();
    }

    public ObservableCollection<PaletteCommand> Results { get; }

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _query = string.Empty;
    [ObservableProperty] private PaletteCommand? _selected;

    public void SetCommands(IEnumerable<PaletteCommand> commands)
    {
        _all.Clear();
        _all.AddRange(commands);
        Refresh();
    }

    partial void OnQueryChanged(string value) => Refresh();

    private void Refresh()
    {
        var q = Query?.Trim() ?? string.Empty;
        IEnumerable<PaletteCommand> matches = q.Length == 0
            ? _all
            : _all.Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                           || c.Hint.Contains(q, StringComparison.OrdinalIgnoreCase));

        Results.Clear();
        foreach (var c in matches) Results.Add(c);
        Selected = Results.FirstOrDefault();
    }

    public void Open()
    {
        Query = string.Empty;
        Refresh();
        IsOpen = true;
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private void Execute(PaletteCommand? command)
    {
        var cmd = command ?? Selected;
        if (cmd is null) return;
        IsOpen = false;
        cmd.Run();
    }

    public void ExecuteSelected() => Execute(Selected);
}
