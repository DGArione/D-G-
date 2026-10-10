using CommunityToolkit.Mvvm.ComponentModel;

namespace DIG.App.ViewModels;

/// <summary>Metadata for a single browser tab. The WebView2 control itself is owned
/// by the main window and keyed by <see cref="Id"/>.</summary>
public sealed partial class BrowserTabViewModel : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private string _title = "New tab";

    [ObservableProperty]
    private string _url = "about:blank";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _canGoBack;

    [ObservableProperty]
    private bool _canGoForward;

    [ObservableProperty]
    private bool _isPrivate;

    [ObservableProperty]
    private bool _isNewTab;

    [ObservableProperty]
    private string? _faviconUrl;
}
