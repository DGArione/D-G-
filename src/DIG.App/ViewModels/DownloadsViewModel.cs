using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Web.WebView2.Core;

namespace DIG.App.ViewModels;

public sealed partial class DownloadItem : ObservableObject
{
    private readonly CoreWebView2DownloadOperation _op;

    public DownloadItem(CoreWebView2DownloadOperation op)
    {
        _op = op;
        _fileName = SafeName(op.ResultFilePath);
        _url = op.Uri ?? string.Empty;
        Update();
        op.BytesReceivedChanged += (_, _) => Update();
        op.StateChanged += (_, _) => Update();
    }

    [ObservableProperty] private string _fileName;
    [ObservableProperty] private string _url;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isComplete;

    public string LocalPath => _op.ResultFilePath ?? string.Empty;

    private void Update()
    {
        double recv = _op.BytesReceived;
        var total = _op.TotalBytesToReceive;
        Progress = total is ulong t && t > 0 ? Math.Clamp(recv / t, 0, 1) : 0;

        switch (_op.State)
        {
            case CoreWebView2DownloadState.InProgress:
                Status = total is ulong tt && tt > 0
                    ? $"{recv / 1048576.0:0.0} / {tt / 1048576.0:0.0} MB"
                    : $"{recv / 1048576.0:0.0} MB";
                IsComplete = false;
                break;
            case CoreWebView2DownloadState.Completed:
                Status = "Completed";
                Progress = 1;
                IsComplete = true;
                break;
            case CoreWebView2DownloadState.Interrupted:
                Status = "Interrupted";
                IsComplete = false;
                break;
        }
    }

    private static string SafeName(string? path)
        => string.IsNullOrEmpty(path) ? "download" : Path.GetFileName(path);

    [RelayCommand]
    private void OpenFile()
    {
        try { if (File.Exists(LocalPath)) Process.Start(new ProcessStartInfo(LocalPath) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        try { if (File.Exists(LocalPath)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{LocalPath}\"")); }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private void Cancel()
    {
        try { _op.Cancel(); } catch { /* ignore */ }
    }
}

public sealed partial class DownloadsViewModel : ObservableObject
{
    public ObservableCollection<DownloadItem> Items { get; } = new();

    public void Track(CoreWebView2DownloadOperation op) => Items.Insert(0, new DownloadItem(op));

    [RelayCommand]
    private void Clear() => Items.Clear();
}
