using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.Core.Data;
using DIG.Core.Models;

namespace DIG.App.ViewModels;

public sealed partial class SshViewModel : ObservableObject
{
    private readonly SshProfileRepository _repo;

    public SshViewModel(SshProfileRepository repo)
    {
        _repo = repo;
        Profiles = new ObservableCollection<SshProfile>();
        Reload();
    }

    public ObservableCollection<SshProfile> Profiles { get; }
    public IReadOnlyList<string> Kinds { get; } = new[] { "ssh", "rdp", "sftp" };

    [ObservableProperty] private SshProfile? _selected;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editHost = string.Empty;
    [ObservableProperty] private string _editPort = "22";
    [ObservableProperty] private string _editUsername = string.Empty;
    [ObservableProperty] private string _editKind = "ssh";
    [ObservableProperty] private string _editNotes = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    partial void OnSelectedChanged(SshProfile? p)
    {
        if (p is null) return;
        EditName = p.Name;
        EditHost = p.Host;
        EditPort = p.Port.ToString();
        EditUsername = p.Username ?? "";
        EditKind = p.Kind;
        EditNotes = p.Notes ?? "";
    }

    public void Reload()
    {
        Profiles.Clear();
        foreach (var p in _repo.GetAll()) Profiles.Add(p);
    }

    private SshProfile Current()
    {
        int.TryParse(EditPort, out var port);
        if (port <= 0) port = EditKind == "rdp" ? 3389 : 22;
        return new SshProfile(Selected?.Id ?? 0,
            string.IsNullOrWhiteSpace(EditName) ? EditHost : EditName.Trim(),
            EditHost.Trim(), port,
            string.IsNullOrWhiteSpace(EditUsername) ? null : EditUsername.Trim(),
            EditKind,
            string.IsNullOrWhiteSpace(EditNotes) ? null : EditNotes.Trim(),
            DateTimeOffset.UtcNow);
    }

    [RelayCommand]
    private void NewProfile()
    {
        Selected = null;
        EditName = "New connection"; EditHost = ""; EditPort = "22"; EditUsername = "";
        EditKind = "ssh"; EditNotes = "";
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(EditHost)) { Status = "Enter a host."; return; }
        var id = _repo.Upsert(Current());
        Reload();
        Selected = Profiles.FirstOrDefault(p => p.Id == id);
        Status = "Saved.";
    }

    [RelayCommand]
    private void Delete()
    {
        if (Selected is { Id: > 0 } p) { _repo.Delete(p.Id); Selected = null; Reload(); Status = "Deleted."; }
    }

    [RelayCommand]
    private void Connect(SshProfile? p)
    {
        var prof = p ?? (string.IsNullOrWhiteSpace(EditHost) ? null : Current());
        if (prof is null || string.IsNullOrWhiteSpace(prof.Host)) { Status = "No host to connect to."; return; }

        try
        {
            switch (prof.Kind)
            {
                case "rdp":
                    Process.Start(new ProcessStartInfo("mstsc.exe", $"/v:{prof.Host}:{prof.Port}") { UseShellExecute = true });
                    break;
                case "sftp":
                    Launch("sftp", prof.Username is null ? $"-P {prof.Port} {prof.Host}" : $"-P {prof.Port} {prof.Username}@{prof.Host}");
                    break;
                default: // ssh
                    Launch("ssh", prof.Username is null ? $"-p {prof.Port} {prof.Host}" : $"-p {prof.Port} {prof.Username}@{prof.Host}");
                    break;
            }
            Status = $"Launching {prof.Kind} → {prof.Target}";
        }
        catch (Exception ex)
        {
            Status = $"Could not launch: {ex.Message}";
        }
    }

    private static void Launch(string exe, string args)
    {
        // Open in a new console window so the interactive session is visible.
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c start \"\" {exe} {args}") { UseShellExecute = true });
    }
}
