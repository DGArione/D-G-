using DIG.App.ViewModels;

namespace DIG.App.Services;

/// <summary>
/// Implemented by the main window to let view-models drive the WebView2-backed
/// browser without taking a direct dependency on the view. WebView2 is inherently
/// view-bound, so the actual control management lives in the window code-behind.
/// </summary>
public interface IBrowserController
{
    void NewTab(string? url = null, bool activate = true);
    void NewPrivateTab(string? url = null);
    void CloseTab(BrowserTabViewModel tab);
    void CloseActiveTab();
    void Activate(BrowserTabViewModel tab);

    void ZoomIn();
    void ZoomOut();
    void ZoomReset();
    void FocusAddressBar();
    void FindInPage(string text, bool forward);

    /// <summary>Opens each URL as a new tab (used by Workspaces).</summary>
    void OpenTabs(IEnumerable<string> urls);

    /// <summary>Interprets omnibox text (URL vs search) and navigates the active tab.</summary>
    void NavigateFromOmnibox(string text);

    void NavigateActive(string url);

    /// <summary>Runs JavaScript in the active tab and returns the JSON-encoded result
    /// (or null if there is no active tab). Used to import data from a rendered page.</summary>
    Task<string?> ExecuteScriptOnActiveTabAsync(string script);

    /// <summary>The active tab's current URL, or null.</summary>
    string? ActiveUrl { get; }

    void Back();
    void Forward();
    void Reload();
    void Stop();
    void GoHome();

    // ---- Chrome / Brave-style extras ----

    /// <summary>Re-opens the most recently closed tab (Ctrl+Shift+T).</summary>
    void ReopenClosedTab();

    /// <summary>Opens a copy of the given tab (or the active tab when null).</summary>
    void DuplicateTab(BrowserTabViewModel? tab);

    /// <summary>Pins / unpins a tab.</summary>
    void TogglePin(BrowserTabViewModel? tab);

    /// <summary>Mutes / unmutes audio for a tab.</summary>
    void ToggleMute(BrowserTabViewModel? tab);

    /// <summary>Closes every tab except the given one.</summary>
    void CloseOtherTabs(BrowserTabViewModel? tab);

    /// <summary>Activates the next / previous tab (Ctrl+Tab / Ctrl+Shift+Tab).</summary>
    void NextTab();
    void PreviousTab();

    /// <summary>Opens the system print dialog for the active page (Ctrl+P).</summary>
    void Print();

    /// <summary>Toggles a distraction-free reader view on the active page.</summary>
    void ToggleReaderMode();

    /// <summary>Toggles borderless full-screen (F11).</summary>
    void ToggleFullScreen();
}
