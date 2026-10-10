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
    private readonly ILogger<MainWindow> _logger;
    private readonly DispatcherTimer _clockTimer;

    private readonly Dictionary<Guid, WebView2> _views = new();
    private readonly List<(string url, bool activate)> _pending = new();
    private CoreWebView2Environment? _env;
    private bool _envReady;

    public MainWindow(MainViewModel vm, AppSettingsService settings, HistoryRepository history, ILogger<MainWindow> logger)
    {
        _vm = vm;
        _settings = settings;
        _history = history;
        _logger = logger;

        InitializeComponent();
        DataContext = _vm;
        _vm.Browser = this;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => _vm.UpdateClocks();
        _clockTimer.Start();

        _vm.CommandPalette.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CommandPaletteViewModel.IsOpen) && _vm.CommandPalette.IsOpen)
                Dispatcher.BeginInvoke(() => { PaletteInput.Focus(); PaletteInput.SelectAll(); });
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
                _pending.Add((_settings.Homepage, true));

            var pending = _pending.ToList();
            _pending.Clear();
            foreach (var (url, activate) in pending)
                await CreateTabAsync(url, activate);
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
        var target = string.IsNullOrWhiteSpace(url) ? _settings.Homepage : url!;
        if (!_envReady)
        {
            _pending.Add((target, activate));
            return;
        }
        _ = CreateTabAsync(target, activate);
    }

    private async Task CreateTabAsync(string url, bool activate)
    {
        if (_env is null) return;

        var tab = new BrowserTabViewModel { Url = url, Title = "New tab" };
        var wv = new WebView2 { Visibility = Visibility.Collapsed };
        _views[tab.Id] = wv;
        BrowserHost.Children.Add(wv);
        _vm.Tabs.Add(tab);

        wv.NavigationStarting += (_, e) =>
        {
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
            if (e.IsSuccess && !string.IsNullOrWhiteSpace(core.Source) && core.Source != "about:blank")
            {
                try { _history.Add(core.Source, tab.Title); } catch { /* history is best-effort */ }
            }
        };

        try
        {
            await wv.EnsureCoreWebView2Async(_env);
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

    public void Back() => WithActiveCore(c => { if (c.CanGoBack) c.GoBack(); });
    public void Forward() => WithActiveCore(c => { if (c.CanGoForward) c.GoForward(); });
    public void Reload() => WithActiveCore(c => c.Reload());
    public void Stop() => WithActiveCore(c => c.Stop());
    public void GoHome() => NavigateActive(_settings.Homepage);

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
