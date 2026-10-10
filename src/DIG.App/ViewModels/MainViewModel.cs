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
    private readonly Dictionary<ModuleId, object> _moduleCache = new();

    public MainViewModel(IServiceProvider sp, DIG.Core.Data.BookmarkRepository bookmarks)
    {
        _sp = sp;
        _bookmarks = bookmarks;

        Tabs = new ObservableCollection<BrowserTabViewModel>();
        CommandPalette = new CommandPaletteViewModel();
        SidebarItems = BuildSidebar();
        SidebarItems[0].IsActive = true; // Browser active on launch
        BuildPalette();
        UpdateClocks();
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
    [ObservableProperty] private string _currentModuleTitle = "Browser";
    [ObservableProperty] private string _omniboxText = string.Empty;
    [ObservableProperty] private string _colomboClock = "--:--";
    [ObservableProperty] private string _utcClock = "--:--";
    [ObservableProperty] private string _securityStatus = "Starting…";

    partial void OnActiveTabChanged(BrowserTabViewModel? value)
    {
        if (value is not null && CurrentModuleId == ModuleId.Browser)
            OmniboxText = value.Url;
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
        new(ModuleId.Workspaces,     "Workspaces",      "", false),
        new(ModuleId.Infrastructure, "Infrastructure",  "", false),
        new(ModuleId.BuildLookup,    "VMware Builds",   "", true),
        new(ModuleId.KnowledgeBase,  "Knowledge Base",  "", false),
        new(ModuleId.AiAssistant,    "AI Assistant",    "", true),
        new(ModuleId.EmailTemplates, "Email Templates", "", true),
        new(ModuleId.Calendar,       "Calendar",        "", false),
        new(ModuleId.Tasks,          "Tasks & Notes",   "", false),
        new(ModuleId.Ssh,            "SSH & Remote",    "", false),
        new(ModuleId.Downloads,      "Downloads",       "", false),
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
        SecurityStatus = "Bookmark added.";
    }

    [RelayCommand]
    private void OpenCommandPalette() => CommandPalette.Open();

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
