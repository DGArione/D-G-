using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DIG.App.Services;
using DIG.App.ViewModels;
using DIG.Core.Ai;
using DIG.Core.Data;
using DIG.Core.Intelligence;
using DIG.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DIG.App.Views;

public partial class MainWindow : Window, IBrowserController
{
    private readonly MainViewModel _vm;
    private readonly AppSettingsService _settings;
    private readonly HistoryRepository _history;
    private readonly DownloadsViewModel _downloads;
    private readonly ILogger<MainWindow> _logger;
    private readonly DispatcherTimer _clockTimer;

    private readonly Dictionary<Guid, WebView2> _views = new();
    private readonly List<(string url, bool activate, bool isPrivate, bool isNewTab)> _pending = new();
    private CoreWebView2Environment? _env;
    private CoreWebView2Environment? _privateEnv;
    private bool _envReady;

    public MainWindow(MainViewModel vm, AppSettingsService settings, HistoryRepository history,
        DownloadsViewModel downloads, ILogger<MainWindow> logger)
    {
        _vm = vm;
        _settings = settings;
        _history = history;
        _downloads = downloads;
        _logger = logger;

        InitializeComponent();
        DataContext = _vm;
        _vm.Browser = this;
        MaximizeFix.Enable(this);

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => _vm.UpdateClocks();
        _clockTimer.Start();

        _vm.CommandPalette.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommandPaletteViewModel.IsOpen) && _vm.CommandPalette.IsOpen)
                Dispatcher.BeginInvoke(() => { PaletteInput.Focus(); PaletteInput.SelectAll(); });
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.FindVisible) && _vm.FindVisible)
                Dispatcher.BeginInvoke(() => { FindBox.Focus(); FindBox.SelectAll(); });
        };

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.SecurityStatus = "Initialising browser engine…";
        try
        {
            _env = await CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.WebView2Dir);
            _envReady = true;
            _vm.SecurityStatus = "WebView2 ready — TLS certificate validation on.";

            if (_pending.Count == 0)
                _pending.Add(("about:blank", true, false, true)); // open the start page

            var pending = _pending.ToList();
            _pending.Clear();
            foreach (var (url, activate, isPrivate, isNewTab) in pending)
            {
                if (isPrivate) await EnsurePrivateEnvAsync();
                await CreateTabAsync(url, activate, isPrivate, isNewTab);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebView2 initialisation failed");
            _vm.SecurityStatus = "WebView2 runtime not available. Install the Microsoft Edge WebView2 Runtime.";
            MessageBox.Show(
                "The Microsoft Edge WebView2 Runtime is required for browsing but could not be initialised.\n\n" +
                "Install it from https://developer.microsoft.com/microsoft-edge/webview2/ and restart D!G!.",
                "D!G!", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // ---- IBrowserController ----

    public void NewTab(string? url = null, bool activate = true)
    {
        var isNewTab = string.IsNullOrWhiteSpace(url);
        var target = isNewTab ? "about:blank" : url!;
        if (!_envReady)
        {
            _pending.Add((target, activate, false, isNewTab));
            return;
        }
        _ = CreateTabAsync(target, activate, false, isNewTab);
    }

    public void NewPrivateTab(string? url = null)
    {
        var isNewTab = string.IsNullOrWhiteSpace(url);
        var target = isNewTab ? "about:blank" : url!;
        if (!_envReady)
        {
            _pending.Add((target, true, true, isNewTab));
            return;
        }
        _ = NewPrivateTabAsync(target, isNewTab);
    }

    private async Task NewPrivateTabAsync(string url, bool isNewTab)
    {
        await EnsurePrivateEnvAsync();
        await CreateTabAsync(url, activate: true, isPrivate: true, isNewTab: isNewTab);
    }

    private async Task EnsurePrivateEnvAsync()
    {
        if (_privateEnv is not null) return;
        var folder = Path.Combine(AppPaths.WebView2Dir, "InPrivate");
        // Start each private session fresh: clear any leftover private profile data.
        try { if (Directory.Exists(folder)) Directory.Delete(folder, true); } catch { }
        _privateEnv = await CoreWebView2Environment.CreateAsync(userDataFolder: folder);
    }

    private async Task CreateTabAsync(string url, bool activate, bool isPrivate = false, bool isNewTab = false)
    {
        var env = isPrivate ? _privateEnv : _env;
        if (env is null) return;

        var tab = new BrowserTabViewModel
        {
            Url = isNewTab ? "" : url,
            Title = isPrivate ? "Private tab" : "New tab",
            IsPrivate = isPrivate,
            IsNewTab = isNewTab
        };
        var wv = new WebView2 { Visibility = Visibility.Collapsed };
        _views[tab.Id] = wv;
        BrowserHost.Children.Add(wv);
        _vm.Tabs.Add(tab);

        wv.NavigationStarting += (_, e) =>
        {
            // HTTPS-only: upgrade plain-http navigations (except localhost) when enabled.
            if (_settings.HttpsOnly
                && e.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !e.Uri.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;
                var https = "https://" + e.Uri["http://".Length..];
                Dispatcher.BeginInvoke(() => { try { wv.CoreWebView2?.Navigate(https); } catch { } });
                return;
            }

            // about:blank is only used to prime a new-tab's WebView; keep the start page.
            if (string.Equals(e.Uri, "about:blank", StringComparison.OrdinalIgnoreCase)) return;
            tab.IsNewTab = false;
            tab.IsLoading = true;
            tab.Url = e.Uri;
            if (tab.IsActive) _vm.OmniboxText = e.Uri;
        };
        wv.NavigationCompleted += (_, e) =>
        {
            tab.IsLoading = false;
            var core = wv.CoreWebView2;
            if (core is null) return;
            tab.CanGoBack = core.CanGoBack;
            tab.CanGoForward = core.CanGoForward;
            tab.Url = core.Source;
            // Private tabs are never written to history.
            if (!tab.IsPrivate && e.IsSuccess && !string.IsNullOrWhiteSpace(core.Source) && core.Source != "about:blank")
            {
                try { _history.Add(core.Source, tab.Title); } catch { /* history is best-effort */ }
            }
        };

        try
        {
            await wv.EnsureCoreWebView2Async(env);
            ConfigureCore(wv.CoreWebView2!, tab);
            if (activate) Activate(tab);
            wv.CoreWebView2!.Navigate(NormalizeUrl(url));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialise a browser tab");
        }
    }

    private void ConfigureCore(CoreWebView2 core, BrowserTabViewModel tab)
    {
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;

        core.DocumentTitleChanged += (_, _) =>
            tab.Title = string.IsNullOrWhiteSpace(core.DocumentTitle) ? "New tab" : core.DocumentTitle;

        core.SourceChanged += (_, _) =>
        {
            tab.Url = core.Source;
            if (tab.IsActive) _vm.OmniboxText = core.Source;
        };

        core.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            NewTab(e.Uri);
        };

        core.FaviconChanged += (_, _) =>
            tab.FaviconUrl = string.IsNullOrEmpty(core.FaviconUri) ? null : core.FaviconUri;

        core.DownloadStarting += (_, e) =>
        {
            try { _downloads.Track(e.DownloadOperation); } catch (Exception ex) { _logger.LogWarning(ex, "download track"); }
        };

        // Ad / tracker blocking: drop sub-resource requests to known tracker domains.
        try
        {
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, e) =>
            {
                if (!_settings.BlockTrackers) return;
                var host = DIG.Core.Security.TrackerBlocklist.HostOf(e.Request.Uri);
                if (DIG.Core.Security.TrackerBlocklist.IsBlocked(host))
                {
                    try { e.Response = core.Environment.CreateWebResourceResponse(null, 403, "Blocked by D!G!", ""); }
                    catch { /* ignore */ }
                }
            };
        }
        catch (Exception ex) { _logger.LogWarning(ex, "resource filter"); }

        core.ContextMenuRequested += OnContextMenuRequested;
    }

    private void OnContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
    {
        try
        {
            var selection = e.ContextMenuTarget?.SelectionText;
            if (string.IsNullOrWhiteSpace(selection) || _env is null) return;

            void Add(string label, Action<string> action)
            {
                var item = _env.CreateContextMenuItem(label, null, CoreWebView2ContextMenuItemKind.Command);
                item.CustomItemSelected += (_, _) => Dispatcher.Invoke(() => action(selection));
                e.MenuItems.Insert(0, item);
            }

            // Inserted top-first, so the visible order is Explain, Summarize, Draft email, Identify build.
            Add("D!G!: Identify VMware build", t => _vm.IdentifyBuild(t));
            Add("D!G!: Draft email from selection", t => _vm.DraftEmailFromSelection(t));
            Add("D!G!: Summarize selection", t => _ = _vm.AskAiAsync(AiPrompts.BuildSummarize(t)));
            Add("D!G!: Explain selection", t => _ = _vm.AskAiAsync(AiPrompts.BuildExplain(t)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build context menu");
        }
    }

    public void CloseTab(BrowserTabViewModel tab)
    {
        if (!_views.TryGetValue(tab.Id, out var wv)) return;

        var wasActive = tab.IsActive;
        BrowserHost.Children.Remove(wv);
        try { wv.Dispose(); } catch { }
        _views.Remove(tab.Id);
        _vm.Tabs.Remove(tab);

        if (_vm.Tabs.Count == 0)
        {
            NewTab();
            return;
        }
        if (wasActive)
            Activate(_vm.Tabs[^1]);
    }

    public void Activate(BrowserTabViewModel tab)
    {
        foreach (var t in _vm.Tabs) t.IsActive = ReferenceEquals(t, tab);
        foreach (var kv in _views)
            kv.Value.Visibility = kv.Key == tab.Id ? Visibility.Visible : Visibility.Collapsed;
        _vm.ActiveTab = tab;
        _vm.OmniboxText = tab.Url;
    }

    public void NavigateFromOmnibox(string text)
    {
        var interpretation = OmniboxInterpreter.Interpret(text, _settings.SearchEngine);
        NavigateActive(interpretation.TargetUrl);
    }

    public void NavigateActive(string url)
    {
        var target = NormalizeUrl(url);
        if (_vm.ActiveTab is null || !_views.TryGetValue(_vm.ActiveTab.Id, out var wv) || wv.CoreWebView2 is null)
        {
            NewTab(target);
            return;
        }
        try { wv.CoreWebView2.Navigate(target); }
        catch (Exception ex) { _logger.LogWarning(ex, "Navigation failed for {Url}", target); }
    }

    public string? ActiveUrl => _vm.ActiveTab?.Url;

    public async Task<string?> ExecuteScriptOnActiveTabAsync(string script)
    {
        if (_vm.ActiveTab is null
            || !_views.TryGetValue(_vm.ActiveTab.Id, out var wv)
            || wv.CoreWebView2 is null)
            return null;
        try
        {
            return await wv.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ExecuteScript failed");
            return null;
        }
    }

    public void Back() => WithActiveCore(c => { if (c.CanGoBack) c.GoBack(); });
    public void Forward() => WithActiveCore(c => { if (c.CanGoForward) c.GoForward(); });
    public void Reload() => WithActiveCore(c => c.Reload());
    public void Stop() => WithActiveCore(c => c.Stop());
    public void GoHome()
    {
        if (_vm.ActiveTab is null) { NewTab(); return; }
        _vm.ActiveTab.IsNewTab = true;   // show the start page
        _vm.OmniboxText = string.Empty;
        WithActiveCore(c => c.Navigate("about:blank"));
    }

    private void WithActiveCore(Action<CoreWebView2> action)
    {
        if (_vm.ActiveTab is not null
            && _views.TryGetValue(_vm.ActiveTab.Id, out var wv)
            && wv.CoreWebView2 is not null)
        {
            try { action(wv.CoreWebView2); }
            catch (Exception ex) { _logger.LogWarning(ex, "Browser action failed"); }
        }
    }

    public void CloseActiveTab()
    {
        if (_vm.ActiveTab is not null) CloseTab(_vm.ActiveTab);
    }

    public void ZoomIn() => AdjustZoom(0.1);
    public void ZoomOut() => AdjustZoom(-0.1);
    public void ZoomReset() => WithActiveView(wv => wv.ZoomFactor = 1.0);

    private void AdjustZoom(double delta)
        => WithActiveView(wv => wv.ZoomFactor = Math.Clamp(wv.ZoomFactor + delta, 0.5, 3.0));

    private void WithActiveView(Action<WebView2> action)
    {
        if (_vm.ActiveTab is not null && _views.TryGetValue(_vm.ActiveTab.Id, out var wv))
        {
            try { action(wv); }
            catch (Exception ex) { _logger.LogWarning(ex, "Browser view action failed"); }
        }
    }

    public void FocusAddressBar()
    {
        Omnibox.Focus();
        Omnibox.SelectAll();
    }

    public void FindInPage(string text, bool forward)
    {
        if (string.IsNullOrEmpty(text)) return;
        var backwards = (!forward).ToString().ToLowerInvariant();
        var js = $"window.find({JsString(text)}, false, {backwards}, true)";
        WithActiveCore(c => _ = c.ExecuteScriptAsync(js));
    }

    private static string JsString(string s)
        => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ") + "\"";

    public void OpenTabs(IEnumerable<string> urls)
    {
        var list = urls.Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
        for (var i = 0; i < list.Count; i++)
            NewTab(list[i], activate: i == 0);
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "about:blank";
        if (url.Contains("://") || url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
            return url;
        return "https://" + url;
    }

    // ---- Caption buttons ----

    private void MinButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaxButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    // ---- Command palette ----

    private void PaletteInput_KeyDown(object sender, KeyEventArgs e)
    {
        var palette = _vm.CommandPalette;
        switch (e.Key)
        {
            case Key.Enter:
                palette.ExecuteSelected();
                e.Handled = true;
                break;
            case Key.Escape:
                palette.CloseCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Down:
                MovePaletteSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                MovePaletteSelection(-1);
                e.Handled = true;
                break;
        }
    }

    private void MovePaletteSelection(int delta)
    {
        var results = _vm.CommandPalette.Results;
        if (results.Count == 0) return;
        var index = _vm.CommandPalette.Selected is { } s ? results.IndexOf(s) : -1;
        index = Math.Clamp(index + delta, 0, results.Count - 1);
        _vm.CommandPalette.Selected = results[index];
        PaletteList.ScrollIntoView(_vm.CommandPalette.Selected);
    }

    private void PaletteList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        => _vm.CommandPalette.ExecuteSelected();

    protected override void OnClosed(EventArgs e)
    {
        _clockTimer.Stop();
        foreach (var wv in _views.Values)
        {
            try { wv.Dispose(); } catch { }
        }
        _views.Clear();
        base.OnClosed(e);
    }
}
