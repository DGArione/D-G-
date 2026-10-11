using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIG.App.Services;
using DIG.Core.Data;
using DIG.Core.Intelligence;
using Microsoft.Extensions.DependencyInjection;

namespace DIG.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;
    private readonly DIG.Core.Data.BookmarkRepository _bookmarks;
    private readonly AppSettingsService _settings;
    private readonly Dictionary<ModuleId, object> _moduleCache = new();

    public MainViewModel(IServiceProvider sp, DIG.Core.Data.BookmarkRepository bookmarks)
    {
        _sp = sp;
        _bookmarks = bookmarks;
        _settings = sp.GetRequiredService<AppSettingsService>();

        Tabs = new ObservableCollection<BrowserTabViewModel>();
        CommandPalette = new CommandPaletteViewModel();
        SidebarItems = BuildSidebar();
        SidebarItems[0].IsActive = true; // Browser active on launch
        _blockTrackers = _settings.BlockTrackers;
        _httpsOnly = _settings.HttpsOnly;
        BuildPalette();
        UpdateClocks();
        RefreshStartBookmarks();
    }

    /// <summary>Set by the main window, which owns the WebView2 controls.</summary>
    public IBrowserController? Browser { get; set; }

    public ObservableCollection<SidebarItemViewModel> SidebarItems { get; }
    public ObservableCollection<BrowserTabViewModel> Tabs { get; }
    public CommandPaletteViewModel CommandPalette { get; }

    [ObservableProperty] private BrowserTabViewModel? _activeTab;
    [ObservableProperty] private ModuleId _currentModuleId = ModuleId.Browser;
    [ObservableProperty] private object? _currentModule;
    [ObservableProperty] private bool _isBrowserVisible = true;
    [ObservableProperty] private bool _showStartPage = true;
    [ObservableProperty] private bool _showWebView;

    // ---- Chrome / Brave-style UI state ----
    [ObservableProperty] private bool _showBookmarksBar = true;
    [ObservableProperty] private bool _isFullScreen;
    [ObservableProperty] private bool _menuOpen;
    [ObservableProperty] private bool _shieldsOpen;
    [ObservableProperty] private bool _blockTrackers;
    [ObservableProperty] private bool _httpsOnly;

    partial void OnBlockTrackersChanged(bool value) => _settings.BlockTrackers = value;
    partial void OnHttpsOnlyChanged(bool value) => _settings.HttpsOnly = value;

    /// <summary>The bookmarks bar shows only when enabled and not in full-screen.</summary>
    public bool BookmarksBarVisible => ShowBookmarksBar && !IsFullScreen;
    partial void OnShowBookmarksBarChanged(bool value) => OnPropertyChanged(nameof(BookmarksBarVisible));
    partial void OnIsFullScreenChanged(bool value) => OnPropertyChanged(nameof(BookmarksBarVisible));

    public ObservableCollection<DIG.Core.Models.Bookmark> StartBookmarks { get; } = new();

    /// <summary>Implemented modules shown as quick tiles on the start page.</summary>
    public IEnumerable<SidebarItemViewModel> StartTiles =>
        SidebarItems.Where(s => s.Implemented && s.Id != ModuleId.Browser);

    private BrowserTabViewModel? _subscribedTab;

    private void UpdateStartPage()
    {
        ShowStartPage = IsBrowserVisible && (ActiveTab?.IsNewTab ?? false);
        // The WebView2 is a native window that always paints over WPF content, so it
        // must be hidden whenever the WPF start page should be visible.
        ShowWebView = IsBrowserVisible && !ShowStartPage;
    }

    partial void OnIsBrowserVisibleChanged(bool value) => UpdateStartPage();

    private void RefreshStartBookmarks()
    {
        StartBookmarks.Clear();
        foreach (var b in _bookmarks.GetAll().Take(12)) StartBookmarks.Add(b);
    }
    [ObservableProperty] private string _currentModuleTitle = "Browser";
    [ObservableProperty] private string _omniboxText = string.Empty;
    [ObservableProperty] private string _colomboClock = "--:--";
    [ObservableProperty] private string _utcClock = "--:--";
    [ObservableProperty] private string _securityStatus = "Starting…";
    [ObservableProperty] private bool _findVisible;
    [ObservableProperty] private string _findText = string.Empty;

    partial void OnActiveTabChanged(BrowserTabViewModel? value)
    {
        if (_subscribedTab is not null) _subscribedTab.PropertyChanged -= OnActiveTabPropertyChanged;
        _subscribedTab = value;
        if (value is not null) value.PropertyChanged += OnActiveTabPropertyChanged;

        if (value is not null && CurrentModuleId == ModuleId.Browser && !value.IsNewTab)
            OmniboxText = value.Url;
        UpdateStartPage();
    }

    private void OnActiveTabPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BrowserTabViewModel.IsNewTab))
            UpdateStartPage();
    }

    public void UpdateClocks()
    {
        var now = DateTimeOffset.UtcNow;
        ColomboClock = TimeZoneHelper.Format(TimeZoneHelper.Colombo, now);
        UtcClock = now.ToString("HH:mm");
    }

    private ObservableCollection<SidebarItemViewModel> BuildSidebar() => new()
    {
        new(ModuleId.Browser,        "Browser",         "", true),
        new(ModuleId.Workspaces,     "Workspaces",      "", true),
        new(ModuleId.Infrastructure, "Infrastructure",  "", true),
        new(ModuleId.BuildLookup,    "VMware Builds",   "", true),
        new(ModuleId.KnowledgeBase,  "Knowledge Base",  "", true),
        new(ModuleId.AiAssistant,    "AI Assistant",    "", true),
        new(ModuleId.EmailTemplates, "Email Templates", "", true),
        new(ModuleId.Calendar,       "Calendar",        "", true),
        new(ModuleId.Tasks,          "Tasks & Notes",   "", true),
        new(ModuleId.Ssh,            "SSH & Remote",    "", true),
        new(ModuleId.Downloads,      "Downloads",       "", true),
        new(ModuleId.Bookmarks,      "Bookmarks",       "", true),
        new(ModuleId.History,        "History",         "", true),
        new(ModuleId.Settings,       "Settings",        "", true),
    };

    [RelayCommand]
    private void SelectModule(ModuleId id)
    {
        CurrentModuleId = id;
        var item = SidebarItems.FirstOrDefault(s => s.Id == id);
        CurrentModuleTitle = item?.Label ?? id.ToString();
        foreach (var s in SidebarItems) s.IsActive = s.Id == id;

        if (id == ModuleId.Browser)
        {
            IsBrowserVisible = true;
            CurrentModule = null;
            if (ActiveTab is not null) OmniboxText = ActiveTab.Url;
            return;
        }

        IsBrowserVisible = false;
        CurrentModule = ResolveModule(id);
    }

    private object ResolveModule(ModuleId id)
    {
        if (_moduleCache.TryGetValue(id, out var cached))
        {
            RefreshModule(id, cached);
            return cached;
        }

        object vm = id switch
        {
            ModuleId.AiAssistant => _sp.GetRequiredService<AiAssistantViewModel>(),
            ModuleId.Settings => CreateSettings(),
            ModuleId.EmailTemplates => _sp.GetRequiredService<EmailTemplatesViewModel>(),
            ModuleId.BuildLookup => CreateBuildLookup(),
            ModuleId.Infrastructure => CreateInfrastructure(),
            ModuleId.Workspaces => CreateWorkspaces(),
            ModuleId.Downloads => _sp.GetRequiredService<DownloadsViewModel>(),
            ModuleId.Tasks => _sp.GetRequiredService<TasksViewModel>(),
            ModuleId.Calendar => _sp.GetRequiredService<CalendarViewModel>(),
            ModuleId.Ssh => _sp.GetRequiredService<SshViewModel>(),
            ModuleId.KnowledgeBase => _sp.GetRequiredService<KnowledgeBaseViewModel>(),
            ModuleId.Bookmarks => CreateWithNavigation(_sp.GetRequiredService<BookmarksViewModel>(), v => ((BookmarksViewModel)v).NavigateRequested += OpenInBrowser),
            ModuleId.History => CreateWithNavigation(_sp.GetRequiredService<HistoryViewModel>(), v => ((HistoryViewModel)v).NavigateRequested += OpenInBrowser),
            _ => new PlaceholderViewModel(
                SidebarItems.First(s => s.Id == id).Label,
                "This module is planned for a future build. See docs/FEATURE_MATRIX.md for the current status. "
                + "D!G! deliberately shows this honest placeholder instead of a non-functional screen."),
        };

        _moduleCache[id] = vm;
        return vm;
    }

    private object CreateSettings()
    {
        var settings = _sp.GetRequiredService<SettingsViewModel>();
        settings.SettingsChanged += OnSettingsChanged;
        return settings;
    }

    private object CreateBuildLookup()
    {
        var vm = _sp.GetRequiredService<BuildLookupViewModel>();
        vm.NavigateRequested += OpenInBrowser;
        vm.ImportFromPageRequested += () => _ = ImportBuildsFromPageAsync(vm);
        return vm;
    }

    private object CreateInfrastructure()
    {
        var vm = _sp.GetRequiredService<InfrastructureViewModel>();
        vm.NavigateRequested += OpenInBrowser;
        return vm;
    }

    private object CreateWorkspaces()
    {
        var vm = _sp.GetRequiredService<WorkspacesViewModel>();
        vm.SaveCurrentRequested += name =>
        {
            var urls = Tabs
                .Select(t => t.Url)
                .Where(u => !string.IsNullOrWhiteSpace(u) && u != "about:blank")
                .Distinct()
                .ToList();
            vm.CommitSaved(name, urls);
        };
        vm.OpenRequested += ws =>
        {
            SelectModule(ModuleId.Browser);
            Browser?.OpenTabs(ws.Urls);
        };
        return vm;
    }

    private async Task ImportBuildsFromPageAsync(BuildLookupViewModel vm)
    {
        if (Browser is null)
        {
            vm.OnPageImported(0, "Browser is not ready yet.");
            return;
        }

        const string script =
            "(function(){var o=[];document.querySelectorAll('table tr').forEach(function(tr){" +
            "var c=[];tr.querySelectorAll('th,td').forEach(function(x){c.push((x.innerText||'').trim());});" +
            "if(c.length)o.push(c);});return o;})()";

        var json = await Browser.ExecuteScriptOnActiveTabAsync(script);
        if (string.IsNullOrWhiteSpace(json) || json == "null")
        {
            vm.OnPageImported(0, "No active browser page to read. Open a Broadcom build-numbers KB page first.");
            return;
        }

        try
        {
            var rows = System.Text.Json.JsonSerializer.Deserialize<List<List<string>>>(json) ?? new();
            var arrays = rows.Select(r => r.ToArray());
            var records = BroadcomTableParser.ParseRows(arrays, GuessProduct(Browser.ActiveUrl));
            var cache = _sp.GetRequiredService<BuildCacheRepository>();
            var n = cache.AddRange(records, "browser-import", "Verified", Browser.ActiveUrl);
            vm.OnPageImported(n, null);
        }
        catch (Exception ex)
        {
            vm.OnPageImported(0, "Could not read the page tables: " + ex.Message);
        }
    }

    private static string? GuessProduct(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var u = url.ToLowerInvariant();
        if (u.Contains("vce") || u.Contains("vcenter")) return "VMware vCenter Server";
        if (u.Contains("esx")) return "VMware ESXi";
        return null;
    }

    private static object CreateWithNavigation(object vm, Action<object> wire)
    {
        wire(vm);
        return vm;
    }

    private void RefreshModule(ModuleId id, object vm)
    {
        switch (vm)
        {
            case BookmarksViewModel b: b.Reload(); break;
            case HistoryViewModel h: h.Reload(); break;
            case WorkspacesViewModel w: w.Reload(); break;
            case TasksViewModel t: t.Reload(); break;
            case CalendarViewModel c: c.Reload(); break;
            case SshViewModel s: s.Reload(); break;
            case KnowledgeBaseViewModel k: k.Reload(); break;
        }
    }

    private void OnSettingsChanged()
    {
        if (_moduleCache.TryGetValue(ModuleId.AiAssistant, out var ai) && ai is AiAssistantViewModel aiVm)
            aiVm.RefreshConfiguration();
        if (_moduleCache.TryGetValue(ModuleId.EmailTemplates, out var em) && em is EmailTemplatesViewModel emVm)
            emVm.RefreshConfiguration();
    }

    private void OpenInBrowser(string url)
    {
        SelectModule(ModuleId.Browser);
        Browser?.NavigateActive(url);
    }

    // ---- Browser commands (delegate to the window-owned controller) ----

    [RelayCommand]
    private void NewTab()
    {
        if (CurrentModuleId != ModuleId.Browser) SelectModule(ModuleId.Browser);
        Browser?.NewTab();
    }
    [RelayCommand] private void CloseTab(BrowserTabViewModel? tab) { if (tab is not null) Browser?.CloseTab(tab); }
    [RelayCommand] private void ActivateTab(BrowserTabViewModel? tab) { if (tab is not null) Browser?.Activate(tab); }
    [RelayCommand] private void GoBack() => Browser?.Back();
    [RelayCommand] private void GoForward() => Browser?.Forward();
    [RelayCommand] private void Reload() => Browser?.Reload();
    [RelayCommand] private void Stop() => Browser?.Stop();
    [RelayCommand] private void GoHome() => Browser?.GoHome();

    [RelayCommand]
    private void Navigate()
    {
        if (CurrentModuleId != ModuleId.Browser) SelectModule(ModuleId.Browser);
        Browser?.NavigateFromOmnibox(OmniboxText);
    }

    [RelayCommand]
    private void AddBookmark()
    {
        if (ActiveTab is null) return;
        _bookmarks.Add(ActiveTab.Title, ActiveTab.Url);
        if (_moduleCache.TryGetValue(ModuleId.Bookmarks, out var bm) && bm is BookmarksViewModel b)
            b.Reload();
        RefreshStartBookmarks();
        SecurityStatus = "Bookmark added.";
    }

    [RelayCommand]
    private void NewPrivateTab()
    {
        if (CurrentModuleId != ModuleId.Browser) SelectModule(ModuleId.Browser);
        Browser?.NewPrivateTab();
    }

    [RelayCommand] private void CloseActiveTab() => Browser?.CloseActiveTab();
    [RelayCommand] private void ZoomIn() => Browser?.ZoomIn();
    [RelayCommand] private void ZoomOut() => Browser?.ZoomOut();
    [RelayCommand] private void ZoomReset() => Browser?.ZoomReset();
    [RelayCommand] private void FocusAddressBar() => Browser?.FocusAddressBar();

    [RelayCommand]
    private void ShowFind()
    {
        if (CurrentModuleId != ModuleId.Browser) SelectModule(ModuleId.Browser);
        FindVisible = true;
    }

    [RelayCommand] private void FindNext() => Browser?.FindInPage(FindText, true);
    [RelayCommand] private void FindPrevious() => Browser?.FindInPage(FindText, false);
    [RelayCommand] private void CloseFind() => FindVisible = false;

    [RelayCommand]
    private void CopyUrl()
    {
        if (ActiveTab is null) return;
        try
        {
            System.Windows.Clipboard.SetText(ActiveTab.Url);
            SecurityStatus = "Page URL copied to clipboard.";
        }
        catch { /* clipboard may be unavailable */ }
    }

    [RelayCommand]
    private void OpenStartBookmark(DIG.Core.Models.Bookmark? b)
    {
        if (b is null) return;
        SelectModule(ModuleId.Browser);
        Browser?.NavigateActive(b.Url);
    }

    [RelayCommand]
    private void OpenCommandPalette() => CommandPalette.Open();

    // ---- Chrome / Brave-style commands ----

    [RelayCommand] private void ToggleBookmarksBar() => ShowBookmarksBar = !ShowBookmarksBar;
    [RelayCommand] private void ToggleMenu() => MenuOpen = !MenuOpen;
    [RelayCommand] private void ToggleShields() => ShieldsOpen = !ShieldsOpen;

    [RelayCommand]
    private void ToggleFullScreen()
    {
        IsFullScreen = !IsFullScreen;
        Browser?.ToggleFullScreen();
    }

    [RelayCommand] private void ReopenClosedTab() { CloseMenu(); Browser?.ReopenClosedTab(); }
    [RelayCommand] private void DuplicateTab(BrowserTabViewModel? tab) => Browser?.DuplicateTab(tab ?? ActiveTab);
    [RelayCommand] private void PinTab(BrowserTabViewModel? tab) => Browser?.TogglePin(tab ?? ActiveTab);
    [RelayCommand] private void MuteTab(BrowserTabViewModel? tab) => Browser?.ToggleMute(tab ?? ActiveTab);
    [RelayCommand] private void CloseOtherTabs(BrowserTabViewModel? tab) => Browser?.CloseOtherTabs(tab ?? ActiveTab);
    [RelayCommand] private void NextTab() => Browser?.NextTab();
    [RelayCommand] private void PreviousTab() => Browser?.PreviousTab();

    [RelayCommand] private void Print() { CloseMenu(); Browser?.Print(); }

    [RelayCommand]
    private void ReaderMode()
    {
        CloseMenu();
        if (CurrentModuleId != ModuleId.Browser) SelectModule(ModuleId.Browser);
        Browser?.ToggleReaderMode();
    }

    /// <summary>Menu entry that routes to an existing module/command, then closes the menu.</summary>
    [RelayCommand]
    private void MenuGo(string? target)
    {
        CloseMenu();
        switch (target)
        {
            case "newtab": NewTab(); break;
            case "private": NewPrivateTab(); break;
            case "history": SelectModule(ModuleId.History); break;
            case "downloads": SelectModule(ModuleId.Downloads); break;
            case "bookmarks": SelectModule(ModuleId.Bookmarks); break;
            case "find": ShowFind(); break;
            case "zoomin": ZoomIn(); break;
            case "zoomout": ZoomOut(); break;
            case "zoomreset": ZoomReset(); break;
            case "settings": SelectModule(ModuleId.Settings); break;
        }
    }

    private void CloseMenu() => MenuOpen = false;

    // ---- Smart selection actions (invoked from the WebView2 context menu) ----

    public async Task AskAiAsync(string prompt)
    {
        SelectModule(ModuleId.AiAssistant);
        if (CurrentModule is AiAssistantViewModel ai)
            await ai.AskAsync(prompt);
    }

    public void IdentifyBuild(string text)
    {
        SelectModule(ModuleId.BuildLookup);
        if (CurrentModule is BuildLookupViewModel b)
        {
            b.Query = text?.Trim() ?? string.Empty;
            if (b.LookupCommand.CanExecute(null))
                b.LookupCommand.Execute(null);
        }
    }

    public void DraftEmailFromSelection(string text)
    {
        SelectModule(ModuleId.EmailTemplates);
        if (CurrentModule is EmailTemplatesViewModel em)
            em.ExtraContext = text ?? string.Empty;
    }

    private void BuildPalette()
    {
        var commands = new List<PaletteCommand>
        {
            new("New tab", "Open a new browser tab", () => NewTab()),
            new("Go: Browser", "Open the browser", () => SelectModule(ModuleId.Browser)),
            new("Go: VMware Build Lookup", "Identify a VMware build number", () => SelectModule(ModuleId.BuildLookup)),
            new("Go: AI Assistant", "Chat with the AI assistant", () => SelectModule(ModuleId.AiAssistant)),
            new("Go: Email Templates", "Draft a support email", () => SelectModule(ModuleId.EmailTemplates)),
            new("Go: Bookmarks", "View bookmarks", () => SelectModule(ModuleId.Bookmarks)),
            new("Go: History", "View browsing history", () => SelectModule(ModuleId.History)),
            new("Go: Settings", "Open settings (API key, theme, search)", () => SelectModule(ModuleId.Settings)),
            new("Bookmark current page", "Save the active tab", () => AddBookmark()),
        };
        CommandPalette.SetCommands(commands);
    }
}
